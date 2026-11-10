using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Common.Connections;
using Terminals.Data;
using Terminals.Data.DB;
using Tests.Connections;

namespace Tests.SqlPersisted
{
    [TestClass]
    public class LegacyHttpMigrationSqlTests : TestsLab
    {
        [TestInitialize]
        public void Setup()
        {
            InitializeTestLab();
        }

        [TestCleanup]
        public void Cleanup()
        {
            ClearTestLab();
        }

        [TestMethod]
        public void ConvertHttpToHttps_KeepsFavoriteAndAppendsGroupMembership()
        {
            var original = CreateTestFavorite();
            original.Name = "Legacy SQL favorite";
            original.ServerName = "legacy.invalid";
            TestConnectionManager.Instance.ChangeProtocol(original, KnownConnectionConstants.HTTPS);
            original.Port = 80;
            PrimaryFavorites.Add(original);

            IGroup source = PrimaryFactory.CreateGroup("HTTP");
            PrimaryPersistence.Groups.Add(source);
            PrimaryFavorites.UpdateFavorite(original, new List<IGroup> { source });

            // Simulate a valid historical HTTP record without re-enabling HTTP creation.
            using (var db = DatabaseConnections.CreateInstance())
                db.Database.ExecuteSqlCommand(
                    "UPDATE dbo.Favorites SET Protocol = @protocol, Port = 80 WHERE Id = @id",
                    new SqlParameter("@protocol", KnownConnectionConstants.HTTP),
                    new SqlParameter("@id", original.Id));

            var migration = new LegacyHttpFavoritesMigration(PrimaryPersistence);
            Assert.AreEqual(1, migration.Count());
            migration.Apply(LegacyHttpMigrationChoice.ConvertToHttps, 1);

            using (var db = DatabaseConnections.CreateInstance())
            {
                var converted = db.Favorites.Include(f => f.Groups).Single(f => f.Id == original.Id);
                Assert.AreEqual(KnownConnectionConstants.HTTPS, converted.Protocol);
                Assert.AreEqual(443, converted.Port);
                Assert.AreEqual("legacy.invalid", converted.ServerName);
                Assert.AreEqual(1, converted.Groups.Count);
                Assert.AreEqual("HTTPS", converted.Groups.Single().Name);
                Assert.IsFalse(db.Groups.Any(g => g.Name == "HTTP"));
                Assert.IsFalse(string.IsNullOrEmpty(db.GetProtocolPropertiesByFavorite(original.Id)));
            }
        }

        [TestMethod]
        public void RemoveHttpFavorites_DeletesOnlyLegacyAndEmptyHttpGroup()
        {
            var legacy = CreateTestFavorite();
            legacy.Name = "Legacy SQL favorite";
            TestConnectionManager.Instance.ChangeProtocol(legacy, KnownConnectionConstants.HTTPS);
            PrimaryFavorites.Add(legacy);
            var other = CreateTestFavorite();
            other.Name = "Unrelated SQL favorite";
            PrimaryFavorites.Add(other);

            var group = PrimaryFactory.CreateGroup("HTTP");
            PrimaryPersistence.Groups.Add(group);
            PrimaryFavorites.UpdateFavorite(legacy, new List<IGroup> { group });

            using (var db = DatabaseConnections.CreateInstance())
                db.Database.ExecuteSqlCommand(
                    "UPDATE dbo.Favorites SET Protocol = @protocol, Port = 80 WHERE Id = @id",
                    new SqlParameter("@protocol", KnownConnectionConstants.HTTP),
                    new SqlParameter("@id", legacy.Id));

            var migration = new LegacyHttpFavoritesMigration(PrimaryPersistence);
            migration.Apply(LegacyHttpMigrationChoice.Remove, 1);

            using (var db = DatabaseConnections.CreateInstance())
            {
                Assert.AreEqual(1, db.Favorites.Count());
                Assert.AreEqual(other.Id, db.Favorites.Single().Id);
                Assert.IsFalse(db.Groups.Any(g => g.Name == "HTTP"));
            }
        }
    }
}
