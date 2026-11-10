using System;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Data;

namespace Tests.Connections
{
    [TestClass]
    public class LegacyHttpFavoritesMigrationTests
    {
        private static readonly XNamespace Ns = FavoritesFile.SERIALIZATION_DEFAULT_NAMESPACE;
        private const string LegacyId = "77d73cbf-d6c4-4264-b474-64143708f4e9";
        private const string SecureId = "566a388e-f43b-4a77-b6d6-45ead1b63d13";
        private const string HttpGroupId = "a5f1bb1e-ff0e-47a0-a678-e2c95eea4c10";

        [TestMethod]
        public void Convert_AppendsToHttpsGroupAndPreservesOtherFavorites()
        {
            var document = Sample();
            LegacyHttpFavoritesMigration.ConvertDocument(document,
                LegacyHttpMigrationChoice.ConvertToHttps, 1);

            var legacy = document.Descendants(Ns + "Favorite")
                .Single(f => (string)f.Attribute("id") == LegacyId);
            Assert.AreEqual("HTTPS", (string)legacy.Element(Ns + "Protocol"));
            Assert.AreEqual("443", (string)legacy.Element(Ns + "Port"));
            Assert.AreEqual("/CaseSensitive?Q=AbC", (string)legacy
                .Element(Ns + "WebOptions").Element(Ns + "RelativeUrl"));
            Assert.IsFalse(document.Descendants(Ns + "Group").Any(g =>
                (string)g.Element(Ns + "Name") == "HTTP"));

            var https = document.Descendants(Ns + "Group")
                .Single(g => (string)g.Element(Ns + "Name") == "HTTPS");
            var memberIds = document.Descendants(Ns + "FavoritesInGroup")
                .Single(g => (string)g.Attribute("groupId") ==
                    (string)https.Attribute("id"))
                .Descendants(Ns + "guid").Select(g => g.Value).ToList();
            CollectionAssert.AreEquivalent(new[] { LegacyId, SecureId }, memberIds);
            Assert.AreEqual("HTTPS", (string)document.Descendants(Ns + "Favorite")
                .Single(f => (string)f.Attribute("id") == SecureId).Element(Ns + "Protocol"));
        }

        [TestMethod]
        public void Remove_DeletesOnlyLegacyFavorites()
        {
            var document = Sample();
            LegacyHttpFavoritesMigration.ConvertDocument(document,
                LegacyHttpMigrationChoice.Remove, 1);

            var favorites = document.Descendants(Ns + "Favorite").ToList();
            Assert.AreEqual(1, favorites.Count);
            Assert.AreEqual(SecureId, (string)favorites.Single().Attribute("id"));
            Assert.IsFalse(document.Descendants(Ns + "Group").Any(g =>
                (string)g.Element(Ns + "Name") == "HTTP"));
            Assert.IsFalse(document.Descendants(Ns + "guid").Any(g => g.Value == LegacyId));
        }

        [TestMethod]
        public void Cancel_DoesNotModifyDocument()
        {
            var document = Sample();
            var baseline = new XDocument(document);
            LegacyHttpFavoritesMigration.ConvertDocument(document, LegacyHttpMigrationChoice.Cancel, 1);
            Assert.IsTrue(XNode.DeepEquals(baseline, document));
        }

        [TestMethod]
        public void StaleSelection_RejectsModification()
        {
            var document = Sample();
            var baseline = new XDocument(document);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                LegacyHttpFavoritesMigration.ConvertDocument(document,
                    LegacyHttpMigrationChoice.ConvertToHttps, 2));
            Assert.IsTrue(XNode.DeepEquals(baseline, document));
        }

        [TestMethod]
        public void CustomPort_IsPreserved()
        {
            var document = Sample();
            var legacy = document.Descendants(Ns + "Favorite")
                .Single(f => (string)f.Attribute("id") == LegacyId);
            legacy.SetElementValue(Ns + "Port", "8443");
            LegacyHttpFavoritesMigration.ConvertDocument(document,
                LegacyHttpMigrationChoice.ConvertToHttps, 1);
            Assert.AreEqual("8443", (string)legacy.Element(Ns + "Port"));
        }

        [TestMethod]
        public void NameConflict_RejectsConversionWithoutChangingDocument()
        {
            var document = Sample();
            var legacy = document.Descendants(Ns + "Favorite")
                .Single(f => (string)f.Attribute("id") == LegacyId);
            legacy.SetElementValue(Ns + "Name", "Secure machine");
            var baseline = new XDocument(document);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                LegacyHttpFavoritesMigration.ConvertDocument(document,
                    LegacyHttpMigrationChoice.ConvertToHttps, 1));
            Assert.IsTrue(XNode.DeepEquals(baseline, document));
        }

        [TestMethod]
        public void UnrelatedMembership_PreventsDeletingSharedHttpGroup()
        {
            var document = Sample();
            var members = document.Descendants(Ns + "FavoritesInGroup")
                .Single(x => (string)x.Attribute("groupId") == HttpGroupId)
                .Element(Ns + "Favorites");
            members.Add(new XElement(Ns + "guid", SecureId));
            LegacyHttpFavoritesMigration.ConvertDocument(document,
                LegacyHttpMigrationChoice.Remove, 1);
            Assert.IsTrue(document.Descendants(Ns + "Group")
                .Any(g => (string)g.Element(Ns + "Name") == "HTTP"));
        }

        private static XDocument Sample()
        {
            return new XDocument(
                new XElement(Ns + "FavoritesFile",
                    new XElement(Ns + "Favorites",
                        new XElement(Ns + "Favorite",
                            new XAttribute("id", LegacyId),
                            new XElement(Ns + "Name", "Legacy machine"),
                            new XElement(Ns + "Protocol", "HTTP"),
                            new XElement(Ns + "Port", "80"),
                            new XElement(Ns + "ServerName", "legacy.invalid"),
                            new XElement(Ns + "WebOptions",
                                new XElement(Ns + "RelativeUrl", "/CaseSensitive?Q=AbC"))),
                        new XElement(Ns + "Favorite",
                            new XAttribute("id", SecureId),
                            new XElement(Ns + "Name", "Secure machine"),
                            new XElement(Ns + "Protocol", "HTTPS"),
                            new XElement(Ns + "Port", "443"),
                            new XElement(Ns + "ServerName", "secure.invalid"))),
                    new XElement(Ns + "Groups",
                        new XElement(Ns + "Group",
                            new XAttribute("id", HttpGroupId),
                            new XElement(Ns + "Name", "HTTP")),
                        new XElement(Ns + "Group",
                            new XAttribute("id", "2599af06-f824-4535-8f9e-39dcb843f6d5"),
                            new XElement(Ns + "Name", "HTTPS"))),
                    new XElement(Ns + "FavoritesInGroups",
                        new XElement(Ns + "FavoritesInGroup",
                            new XAttribute("groupId", HttpGroupId),
                            new XElement(Ns + "Favorites", new XElement(Ns + "guid", LegacyId))),
                        new XElement(Ns + "FavoritesInGroup",
                            new XAttribute("groupId", "2599af06-f824-4535-8f9e-39dcb843f6d5"),
                            new XElement(Ns + "Favorites", new XElement(Ns + "guid", SecureId))))));
        }
    }
}
