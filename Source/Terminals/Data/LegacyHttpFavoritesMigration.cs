using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Terminals.Common.Connections;
using Terminals.Configuration;
using Terminals.Data.DB;

namespace Terminals.Data
{
    internal enum LegacyHttpMigrationChoice
    {
        Cancel,
        Remove,
        ConvertToHttps
    }

    /// <summary>
    /// Migrates retired HTTP favorites in either persistence backend. The existing
    /// favorites cache intentionally hides unsupported protocols, so migration
    /// must inspect the persisted records rather than IFavorites.
    /// </summary>
    internal sealed class LegacyHttpFavoritesMigration
    {
        private readonly IPersistence persistence;

        internal LegacyHttpFavoritesMigration(IPersistence persistence)
        {
            this.persistence = persistence;
        }

        internal int Count()
        {
            if (persistence is FilePersistence)
                return CountFile(Settings.Instance.FileLocations.Favorites);

            if (persistence is SqlPersistence)
            {
                using (var db = DatabaseConnections.CreateInstance())
                    return db.Favorites.Count(f => f.Protocol == KnownConnectionConstants.HTTP);
            }

            return 0;
        }

        internal void Apply(LegacyHttpMigrationChoice choice, int expectedCount)
        {
            if (choice == LegacyHttpMigrationChoice.Cancel)
                return;
            if (expectedCount < 1)
                throw new ArgumentOutOfRangeException("expectedCount");

            if (persistence is FilePersistence)
            {
                MigrateFile(Settings.Instance.FileLocations.Favorites, choice, expectedCount);
                ((FilePersistence)persistence).ReloadAfterLegacyHttpMigration();
            }
            else if (persistence is SqlPersistence)
            {
                MigrateDatabase(choice, expectedCount);
                ((SqlPersistence)persistence).ReloadAfterLegacyHttpMigration();
            }
            else
                throw new NotSupportedException("This favorites store does not support HTTP migration.");
        }

        internal static int CountFile(string path)
        {
            if (!File.Exists(path))
                return 0;

            return LegacyFavorites(XDocument.Load(path)).Count();
        }

        internal static void MigrateFile(string path, LegacyHttpMigrationChoice choice, int expectedCount)
        {
            var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            ConvertDocument(document, choice, expectedCount);

            // Write the new document before touching the original. File.Replace
            // atomically installs it and retains a complete backup of the old XML.
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            string temporary = Path.Combine(directory, "." + Path.GetFileName(path) + "." +
                Guid.NewGuid().ToString("N") + ".tmp");
            string backup = path + ".pre-http-migration-" +
                DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" +
                Guid.NewGuid().ToString("N") + ".bak";
            try
            {
                document.Save(temporary);
                File.Replace(temporary, path, backup);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        internal static void ConvertDocument(XDocument document,
            LegacyHttpMigrationChoice choice, int expectedCount)
        {
            if (choice == LegacyHttpMigrationChoice.Cancel)
                return;
            if (choice != LegacyHttpMigrationChoice.Remove &&
                choice != LegacyHttpMigrationChoice.ConvertToHttps)
                throw new ArgumentOutOfRangeException("choice");

            XNamespace ns = FavoritesFile.SERIALIZATION_DEFAULT_NAMESPACE;
            var legacy = LegacyFavorites(document).ToList();
            if (legacy.Count != expectedCount || legacy.Count == 0)
                throw new InvalidOperationException("The HTTP favorites have changed. Reload and review the migration again.");

            var ids = legacy.Select(f =>
            {
                var id = (string)f.Attribute("id");
                Guid result;
                if (!Guid.TryParse(id, out result))
                    throw new InvalidDataException("An HTTP favorite has an invalid identifier.");
                return result.ToString();
            }).ToList();
            if (ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Count)
                throw new InvalidDataException("Duplicate HTTP favorite identifiers were found.");

            var groupsContainer = document.Descendants(ns + "Groups").FirstOrDefault();
            var groupMemberships = document.Descendants(ns + "FavoritesInGroups").FirstOrDefault();
            if (groupsContainer == null || groupMemberships == null)
                throw new InvalidDataException("The favorites file has no group metadata.");

            var groups = groupsContainer.Elements(ns + "Group").ToList();
            var httpGroup = FindSingleGroup(groups, ns, "HTTP");
            var httpsGroup = FindSingleGroup(groups, ns, "HTTPS");
            var httpIds = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);

            if (choice == LegacyHttpMigrationChoice.ConvertToHttps)
            {
                var existingNames = new HashSet<string>(
                    document.Descendants(ns + "Favorite")
                        .Except(legacy).Select(f => (string)f.Element(ns + "Name"))
                        .Where(n => !string.IsNullOrEmpty(n)), StringComparer.OrdinalIgnoreCase);
                if (legacy.Any(f => existingNames.Contains((string)f.Element(ns + "Name"))))
                    throw new InvalidOperationException("A converted HTTP favorite would duplicate an existing favorite name. Rename the conflicting favorite before conversion.");

                if (httpsGroup == null)
                {
                    var groupId = Guid.NewGuid().ToString();
                    httpsGroup = new XElement(ns + "Group",
                        new XAttribute("id", groupId),
                        new XElement(ns + "Parent", Guid.Empty.ToString()),
                        new XElement(ns + "Name", "HTTPS"));
                    groupsContainer.Add(httpsGroup);
                }

                var targetId = (string)httpsGroup.Attribute("id");
                var target = groupMemberships.Elements(ns + "FavoritesInGroup")
                    .SingleOrDefault(g => String.Equals((string)g.Attribute("groupId"),
                        targetId, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                {
                    target = new XElement(ns + "FavoritesInGroup",
                        new XAttribute("groupId", targetId),
                        new XElement(ns + "Favorites"));
                    groupMemberships.Add(target);
                }
                var targetFavorites = target.Element(ns + "Favorites");
                if (targetFavorites == null)
                    throw new InvalidDataException("The HTTPS group has no membership list.");

                foreach (var favorite in legacy)
                {
                    var protocol = favorite.Element(ns + "Protocol");
                    protocol.Value = KnownConnectionConstants.HTTPS;
                    var port = favorite.Element(ns + "Port");
                    if (port != null && port.Value == "80")
                        port.Value = "443";
                }
                foreach (string id in ids)
                {
                    if (!targetFavorites.Elements(ns + "guid").Any(g =>
                        String.Equals((string)g, id, StringComparison.OrdinalIgnoreCase)))
                        targetFavorites.Add(new XElement(ns + "guid", id));
                }
            }
            else
            {
                foreach (var favorite in legacy)
                    favorite.Remove();
            }

            // Other user-defined group memberships are preserved. Only memberships
            // within a folder literally named HTTP are removed during conversion.
            foreach (var mapping in groupMemberships.Elements(ns + "FavoritesInGroup"))
            {
                bool isSource = httpGroup != null &&
                    String.Equals((string)mapping.Attribute("groupId"),
                        (string)httpGroup.Attribute("id"), StringComparison.OrdinalIgnoreCase);
                if (choice == LegacyHttpMigrationChoice.Remove || isSource)
                {
                    var members = mapping.Element(ns + "Favorites");
                    if (members != null)
                        foreach (var member in members.Elements(ns + "guid")
                            .Where(g => httpIds.Contains((string)g)).ToList())
                            member.Remove();
                }
            }

            if (httpGroup != null)
            {
                var id = (string)httpGroup.Attribute("id");
                var membership = groupMemberships.Elements(ns + "FavoritesInGroup")
                    .FirstOrDefault(g => String.Equals((string)g.Attribute("groupId"),
                        id, StringComparison.OrdinalIgnoreCase));
                bool hasFavorites = membership != null &&
                    membership.Descendants(ns + "guid").Any();
                bool hasChildren = groups.Any(g => String.Equals((string)g.Element(ns + "Parent"),
                    id, StringComparison.OrdinalIgnoreCase));
                if (!hasFavorites && !hasChildren)
                {
                    membership?.Remove();
                    httpGroup.Remove();
                }
            }
        }

        private static IEnumerable<XElement> LegacyFavorites(XDocument document)
        {
            XNamespace ns = FavoritesFile.SERIALIZATION_DEFAULT_NAMESPACE;
            return document.Descendants(ns + "Favorite")
                .Where(f => String.Equals((string)f.Element(ns + "Protocol"),
                    KnownConnectionConstants.HTTP, StringComparison.Ordinal));
        }

        private static XElement FindSingleGroup(List<XElement> groups, XNamespace ns, string name)
        {
            var matches = groups.Where(g => String.Equals(
                (string)g.Element(ns + "Name"), name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 1)
                throw new InvalidOperationException("Multiple groups named " + name +
                    " exist. Rename them before migrating HTTP favorites.");
            return matches.FirstOrDefault();
        }

        private static void MigrateDatabase(LegacyHttpMigrationChoice choice, int expectedCount)
        {
            using (var db = DatabaseConnections.CreateInstance())
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var legacy = db.Favorites
                    .Where(f => f.Protocol == KnownConnectionConstants.HTTP).ToList();
                if (legacy.Count != expectedCount || legacy.Count == 0)
                    throw new InvalidOperationException("The HTTP favorites have changed. Reload and review the migration again.");

                var groups = db.Groups.ToList();
                var httpGroup = FindSingleDbGroup(groups, "HTTP");
                var httpsGroup = FindSingleDbGroup(groups, "HTTPS");

                if (choice == LegacyHttpMigrationChoice.ConvertToHttps)
                {
                    var names = new HashSet<string>(db.Favorites
                        .Where(f => f.Protocol != KnownConnectionConstants.HTTP)
                        .Select(f => f.Name).ToList(), StringComparer.OrdinalIgnoreCase);
                    if (legacy.Any(f => names.Contains(f.Name)))
                        throw new InvalidOperationException("A converted HTTP favorite would duplicate an existing favorite name.");

                    if (httpsGroup == null)
                    {
                        httpsGroup = new DbGroup { Name = "HTTPS" };
                        db.Groups.Add(httpsGroup);
                        db.SaveChanges();
                    }
                    // Preserve all existing non-HTTP group memberships. Appending to
                    // HTTPS uses the same join table as the normal Groups implementation.
                    db.Database.ExecuteSqlCommand(@"
INSERT INTO [dbo].[FavoritesInGroup] ([FavoriteId], [GroupId])
SELECT f.[Id], @target FROM [dbo].[Favorites] f
WHERE f.[Protocol] = @http AND NOT EXISTS
    (SELECT 1 FROM [dbo].[FavoritesInGroup] m
     WHERE m.[FavoriteId] = f.[Id] AND m.[GroupId] = @target)",
                        new SqlParameter("@target", httpsGroup.Id),
                        new SqlParameter("@http", KnownConnectionConstants.HTTP));
                    if (httpGroup != null)
                        db.Database.ExecuteSqlCommand(@"
DELETE m FROM [dbo].[FavoritesInGroup] m
JOIN [dbo].[Favorites] f ON f.[Id] = m.[FavoriteId]
WHERE m.[GroupId] = @source AND f.[Protocol] = @http",
                            new SqlParameter("@source", httpGroup.Id),
                            new SqlParameter("@http", KnownConnectionConstants.HTTP));

                    db.Database.ExecuteSqlCommand(@"
UPDATE [dbo].[Favorites]
SET [Protocol] = @https,
    [Port] = CASE WHEN [Port] = 80 THEN 443 ELSE [Port] END
WHERE [Protocol] = @http",
                        new SqlParameter("@https", KnownConnectionConstants.HTTPS),
                        new SqlParameter("@http", KnownConnectionConstants.HTTP));
                }
                else if (choice == LegacyHttpMigrationChoice.Remove)
                {
                    db.Favorites.RemoveRange(legacy);
                    db.SaveChanges();
                }
                else
                    throw new ArgumentOutOfRangeException("choice");

                if (httpGroup != null)
                {
                    db.Database.ExecuteSqlCommand(@"
DELETE FROM [dbo].[Groups]
WHERE [Id] = @source
AND NOT EXISTS (SELECT 1 FROM [dbo].[FavoritesInGroup] WHERE [GroupId] = @source)
AND NOT EXISTS (SELECT 1 FROM [dbo].[Groups] WHERE [ParentId] = @source)",
                        new SqlParameter("@source", httpGroup.Id));
                }

                transaction.Commit();
            }
        }

        private static DbGroup FindSingleDbGroup(IEnumerable<DbGroup> groups, string name)
        {
            var matches = groups.Where(g => String.Equals(
                g.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 1)
                throw new InvalidOperationException("Multiple groups named " + name +
                    " exist. Rename them before migrating HTTP favorites.");
            return matches.FirstOrDefault();
        }
    }
}
