using System;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Common.Connections;
using Terminals.Common.Converters;
using Terminals.Data;
using Terminals.Data.DB;
using Tests.Connections;

namespace Tests.SqlPersisted
{
    [TestClass]
    public class CitrixWorkspaceSqlTests : TestsLab
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
        public void ModernCitrixFavorite_PreservesUrlResourceModeAndProtocol()
        {
            var favorite = CreateTestFavorite();
            favorite.Name = "Citrix SQL favorite";
            TestConnectionManager.Instance.ChangeProtocol(favorite, KnownConnectionConstants.CITRIX_WORKSPACE);
            var options = (CitrixWorkspaceOptions)favorite.ProtocolProperties;
            options.LaunchMode = "StoreFront";
            options.ResourceName = "Finance Desktop";
            UrlConverter.UpdateFavoriteUrl(favorite,
                "https://store.example.invalid:9443/Citrix/Store/discovery?Case=AbC");
            PrimaryFavorites.Add(favorite);

            using (var db = DatabaseConnections.CreateInstance())
            {
                var stored = db.Favorites.Single(f => f.Id == favorite.Id);
                Assert.AreEqual(KnownConnectionConstants.CITRIX_WORKSPACE, stored.Protocol);
                Assert.AreEqual(9443, stored.Port);
                Assert.AreEqual("store.example.invalid", stored.ServerName);
                var serialized = db.GetProtocolPropertiesByFavorite(favorite.Id);
                StringAssert.Contains(serialized, "<ResourceName>Finance Desktop</ResourceName>");
                StringAssert.Contains(serialized, "<LaunchMode>StoreFront</LaunchMode>");
                StringAssert.Contains(serialized, "<RelativeUrl>/Citrix/Store/discovery?Case=AbC</RelativeUrl>");
            }
        }

        [TestMethod]
        public void TruncatedLegacyIcaSqlFavorite_IsLoadedWithItsOriginalOptions()
        {
            var original = CreateTestFavorite();
            original.Name = "Legacy ICA SQL entry";
            PrimaryFavorites.Add(original);
            const string oldOptions = "<ICAOptions><ApplicationName>LegacyApp</ApplicationName>" +
                "<ApplicationPath></ApplicationPath><ClientINI></ClientINI><ServerINI></ServerINI>" +
                "<EnableEncryption>false</EnableEncryption><EncryptionLevel></EncryptionLevel>" +
                "</ICAOptions>";
            using (var db = DatabaseConnections.CreateInstance())
            {
                db.Database.ExecuteSqlCommand(
                    "UPDATE dbo.Favorites SET Protocol = @protocol WHERE Id = @id",
                    new System.Data.SqlClient.SqlParameter("@protocol", KnownConnectionConstants.LEGACY_ICA_SQL),
                    new System.Data.SqlClient.SqlParameter("@id", original.Id));
                db.UpdateFavoriteProtocolProperties(original.Id, oldOptions);
            }

            var reloaded = TestsLab.CreateSqlPersistence(TestConnectionManager.Instance);
            var legacy = reloaded.Favorites.Single(f => f.Id == ((IFavorite)original).Id);
            Assert.AreEqual(KnownConnectionConstants.LEGACY_ICA_SQL, legacy.Protocol);
            var options = legacy.ProtocolProperties as ICAOptions;
            Assert.IsNotNull(options);
            Assert.AreEqual("LegacyApp", options.ApplicationName);
            Assert.ThrowsExactly<NotSupportedException>(() =>
                TestConnectionManager.Instance.CreateConnection(legacy));
            using (var db = DatabaseConnections.CreateInstance())
            {
                XElement expected = XElement.Parse(oldOptions);
                XElement actual = XElement.Parse(db.GetProtocolPropertiesByFavorite(original.Id));
                Assert.AreEqual(expected.Name, actual.Name);
                // SQL XML serialization normalizes <Tag></Tag> into <Tag />.
                // Compare names and text values, including every empty option.
                CollectionAssert.AreEqual(
                    expected.Elements().Select(e => e.Name.LocalName + "=" + e.Value).ToArray(),
                    actual.Elements().Select(e => e.Name.LocalName + "=" + e.Value).ToArray(),
                    "SQL storage must preserve every legacy ICA option and its value.");
            }
        }

        [TestMethod]
        public void LegacyIcaFavorite_RemainsKnownButCannotLaunch()
        {
            var manager = TestConnectionManager.Instance;
            Assert.IsTrue(manager.IsKnownProtocol("ICA Citrix"));
            Assert.IsFalse(manager.GetAvailableProtocols().Contains("ICA Citrix"));
            Assert.ThrowsExactly<NotSupportedException>(() =>
                manager.CreateConnection(new Favorite { Protocol = "ICA Citrix" }));
        }
    }
}
