using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Common.Configuration;
using Terminals.Plugins.Putty;

namespace Tests.Putty
{
    [TestClass]
    public class PuttyTests
    {
        private string root;

        [TestInitialize]
        public void Initialize()
        {
            root = Path.Combine(Path.GetTempPath(), "TerminalsExecutableTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            ExternalExecutablePaths.Putty = null;
            ExternalExecutablePaths.Pageant = null;
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [TestMethod]
        public void ExplicitPath_TakesPrecedenceOverUserAndSystemPath()
        {
            string preferred = CreateExecutable("configured", "putty.exe");
            string user = CreateExecutable("user", "putty.exe");
            string machine = CreateExecutable("machine", "putty.exe");
            Assert.AreEqual(preferred, ExternalExecutablePaths.Find("putty.exe", preferred,
                Path.GetDirectoryName(user), Path.GetDirectoryName(machine)));
        }

        [TestMethod]
        public void InvalidExplicitPath_DoesNotFallBackToPath()
        {
            string user = CreateExecutable("user", "putty.exe");
            string invalid = Path.Combine(root, "missing", "putty.exe");
            Assert.ThrowsExactly<FileNotFoundException>(() => ExternalExecutablePaths.Find(
                "putty.exe", invalid, Path.GetDirectoryName(user), ""));
        }

        [TestMethod]
        public void UserPath_HasPrecedenceOverMachinePath()
        {
            string user = CreateExecutable("user", "putty.exe");
            string machine = CreateExecutable("machine", "putty.exe");
            Assert.AreEqual(user, ExternalExecutablePaths.Find("putty.exe", null,
                Path.GetDirectoryName(user), Path.GetDirectoryName(machine)));
        }

        [TestMethod]
        public void MachinePath_IsUsedWhenUserPathHasNoMatch()
        {
            string machine = CreateExecutable("machine", "pageant.exe");
            Assert.AreEqual(machine, ExternalExecutablePaths.Find("pageant.exe", "",
                Path.Combine(root, "missing-user"), Path.GetDirectoryName(machine)));
        }

        [TestMethod]
        public void Pageant_IsDiscoveredFromPath()
        {
            string user = CreateExecutable("user", "pageant.exe");
            Assert.AreEqual(user, ExternalExecutablePaths.Find("pageant.exe", null,
                Path.GetDirectoryName(user), ""));
        }

        [TestMethod]
        public void MissingExecutable_ReturnsNull()
        {
            Assert.IsNull(ExternalExecutablePaths.Find("putty.exe", null,
                Path.Combine(root, "missing-user"), Path.Combine(root, "missing-machine")));
        }

        [TestMethod]
        public void EmptyMalformedAndDuplicatePathEntries_AreIgnored()
        {
            string expected = CreateExecutable("user", "putty.exe");
            string missing = Path.Combine(root, "missing");
            string path = " ; relative-directory; " + missing + ";" + missing +
                ";" + Path.GetDirectoryName(expected);
            Assert.AreEqual(expected, ExternalExecutablePaths.Find("putty.exe", null, path, ""));
        }

        [TestMethod]
        public void PathDiscovery_RequiresExecutableFilename()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                ExternalExecutablePaths.Find(@"..\putty.exe", null, "", ""));
        }

        [TestMethod]
        public void PuttyConnection_UsesConfiguredExecutable()
        {
            string expected = CreateExecutable("configured", "putty.exe");
            ExternalExecutablePaths.Putty = expected;
            Assert.AreEqual(expected, Executables.GetPuttyBinaryPath());
        }

        [TestMethod]
        public void Pageant_UsesConfiguredExecutable()
        {
            string expected = CreateExecutable("configured", "pageant.exe");
            ExternalExecutablePaths.Pageant = expected;
            Assert.AreEqual(expected, Executables.GetPageantBinaryPath());
        }

        private string CreateExecutable(string folder, string fileName)
        {
            string directory = Path.Combine(root, folder);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, "dummy executable for path resolution only");
            return path;
        }
    }
}
