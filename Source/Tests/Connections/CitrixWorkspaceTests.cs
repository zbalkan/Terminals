using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Common.Configuration;
using Terminals.Common.Connections;
using Terminals.Common.Converters;
using Terminals.Connections.ICA;
using Terminals.Data;

namespace Tests.Connections
{
    [TestClass]
    public class CitrixWorkspaceTests
    {
        private string directory;

        [TestInitialize]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "CitrixWorkspaceTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            ExternalExecutablePaths.CitrixSelfService = null;
            ExternalExecutablePaths.CitrixStorebrowse = null;
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        [TestMethod]
        public void Cloud_UsesSelfServiceQuickLaunchWithoutCredentials()
        {
            string exe = MakeExecutable("SelfService.exe");
            ExternalExecutablePaths.CitrixSelfService = exe;
            var options = new CitrixWorkspaceOptions
            {
                LaunchMode = "Cloud",
                ResourceName = "Accounting Desktop"
            };
            var start = CitrixWorkspaceConnection.BuildStartInfo(options,
                new Uri("https://store.example.invalid/Citrix/Store/discovery"));
            Assert.AreEqual(exe, start.FileName);
            Assert.AreEqual("storebrowse -q \"Accounting Desktop\" \"https://store.example.invalid/Citrix/Store/discovery\"", start.Arguments);
            Assert.IsFalse(start.UseShellExecute);
            Assert.IsFalse(start.Arguments.Contains("-P "));
        }

        [TestMethod]
        public void StoreFront_UsesStorebrowseSessionLaunchWithoutCredentials()
        {
            string exe = MakeExecutable("storebrowse.exe");
            ExternalExecutablePaths.CitrixStorebrowse = exe;
            var options = new CitrixWorkspaceOptions
            {
                LaunchMode = "StoreFront",
                ResourceName = "Published Application"
            };
            var start = CitrixWorkspaceConnection.BuildStartInfo(options,
                new Uri("https://gateway.example.invalid/Citrix/Store/discovery"));
            Assert.AreEqual(exe, start.FileName);
            Assert.AreEqual("-S \"Published Application\" \"https://gateway.example.invalid/Citrix/Store/discovery\"", start.Arguments);
        }

        [TestMethod]
        public void MissingExecutable_IsReportedWithoutStartingSession()
        {
            ExternalExecutablePaths.CitrixSelfService = Path.Combine(directory, "missing.exe");
            var options = new CitrixWorkspaceOptions { LaunchMode = "Cloud", ResourceName = "Desktop" };
            Assert.ThrowsExactly<FileNotFoundException>(() =>
                CitrixWorkspaceConnection.BuildStartInfo(options, new Uri("https://store.example.invalid/")));
        }

        [TestMethod]
        public void UnsafeStoreUrlAndInvalidMode_AreRejected()
        {
            MakeExecutable("SelfService.exe");
            var options = new CitrixWorkspaceOptions { LaunchMode = "Cloud", ResourceName = "Desktop" };
            Assert.ThrowsExactly<ArgumentException>(() =>
                CitrixWorkspaceConnection.BuildStartInfo(options, new Uri("http://store.example.invalid/")));
            Assert.ThrowsExactly<ArgumentException>(() =>
                CitrixWorkspaceConnection.BuildStartInfo(options, new Uri("https://user:pass@store.example.invalid/")));
            options.LaunchMode = "unsupported";
            Assert.ThrowsExactly<ArgumentException>(() =>
                CitrixWorkspaceConnection.BuildStartInfo(options, new Uri("https://store.example.invalid/")));
        }

        [TestMethod]
        public void ResourceName_CannotInjectAnotherArgument()
        {
            ExternalExecutablePaths.CitrixSelfService = MakeExecutable("SelfService.exe");
            var options = new CitrixWorkspaceOptions
            {
                LaunchMode = "Cloud",
                ResourceName = "App\" -P password"
            };
            var start = CitrixWorkspaceConnection.BuildStartInfo(options,
                new Uri("https://store.example.invalid/"));
            Assert.IsTrue(start.Arguments.Contains("App\\\" -P password"));
            options.ResourceName = "App\r\n-P password";
            Assert.ThrowsExactly<ArgumentException>(() =>
                CitrixWorkspaceConnection.BuildStartInfo(options, new Uri("https://store.example.invalid/")));
        }

        [TestMethod]
        public void CitrixFavorite_RoundTripsHttpsStorePathAndPort()
        {
            var favorite = new Favorite
            {
                Protocol = KnownConnectionConstants.CITRIX_WORKSPACE,
                ProtocolProperties = new CitrixWorkspaceOptions { LaunchMode = "StoreFront", ResourceName = "Finance" }
            };
            UrlConverter.UpdateFavoriteUrl(favorite, "https://store.example.invalid:8443/Citrix/Store/discovery?Case=X");
            var result = UrlConverter.ExtractAbsoluteUrl(favorite);
            Assert.AreEqual("https://store.example.invalid:8443/Citrix/Store/discovery?Case=X", result);
            Assert.AreEqual(8443, favorite.Port);
            Assert.ThrowsExactly<ArgumentException>(() =>
                UrlConverter.UpdateFavoriteUrl(favorite, "http://store.example.invalid/"));
        }

        [TestMethod]
        public void LegacyIca_IsKnownButNotLaunchable()
        {
            var manager = TestConnectionManager.Instance;
            Assert.IsTrue(manager.IsKnownProtocol(ICAConnectionPlugin.ICA_CITRIX));
            Assert.IsFalse(Array.Exists(manager.GetAvailableProtocols(), x => x == ICAConnectionPlugin.ICA_CITRIX));
            Assert.ThrowsExactly<NotSupportedException>(() =>
                manager.CreateConnection(new Favorite { Protocol = ICAConnectionPlugin.ICA_CITRIX }));
        }

        private string MakeExecutable(string name)
        {
            string path = Path.Combine(directory, name);
            File.WriteAllText(path, "test executable");
            return path;
        }
    }
}
