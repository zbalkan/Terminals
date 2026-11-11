using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Common.Configuration;
using Terminals.Connections;
using Terminals.Data;

namespace Tests.Connections
{
    [TestClass]
    public class ExternalVncTests
    {
        private string directory;

        [TestInitialize]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "ExternalVncTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            ExternalVncViewers.Selected = ExternalVncViewers.TightVnc;
            ExternalExecutablePaths.VncViewer = Path.Combine(directory, "tvnviewer.exe");
            File.WriteAllText(ExternalExecutablePaths.VncViewer, "test executable");
        }

        [TestCleanup]
        public void Cleanup()
        {
            ExternalExecutablePaths.VncViewer = null;
            ExternalVncViewers.Selected = ExternalVncViewers.TightVnc;
            ExternalVncViewers.UltraVncPath = null;
            ExternalVncViewers.TigerVncPath = null;
            ExternalVncViewers.RealVncPath = null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [TestMethod]
        public void Launcher_UsesExplicitPortAndPreservesViewerAuthentication()
        {
            var options = new VncOptions { ViewOnly = true, AutoScale = true, DisplayNumber = 8 };
            var start = VNCConnection.BuildStartInfo("desktop.example.invalid", 5999, options);
            Assert.AreEqual(ExternalExecutablePaths.VncViewer, start.FileName);
            Assert.AreEqual("-viewonly=yes -scale=auto \"desktop.example.invalid::5999\"", start.Arguments);
            Assert.IsFalse(start.UseShellExecute);
            Assert.IsFalse(start.Arguments.Contains("password"));
        }

        [TestMethod]
        public void Launcher_RejectsInvalidDestination()
        {
            Assert.ThrowsExactly<ArgumentException>(() => VNCConnection.BuildStartInfo("", 5900, null));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                VNCConnection.BuildStartInfo("server.example.invalid", 0, null));
            Assert.ThrowsExactly<ArgumentException>(() =>
                VNCConnection.BuildStartInfo("server\" -password=secret", 5900, null));
        }

        [TestMethod]
        public void Launcher_ReportsMissingViewer()
        {
            File.Delete(ExternalExecutablePaths.VncViewer);
            Assert.ThrowsExactly<FileNotFoundException>(() =>
                VNCConnection.BuildStartInfo("server.example.invalid", 5900, null));
        }

        [TestMethod]
        public void UltraVnc_UsesItsOwnViewerAndCommandLine()
        {
            ExternalVncViewers.Selected = ExternalVncViewers.UltraVnc;
            ExternalVncViewers.UltraVncPath = MakeExecutable("Ultra", "vncviewer.exe");
            var options = new VncOptions { ViewOnly = true, AutoScale = true };
            var start = VNCConnection.BuildStartInfo("desktop.example.invalid", 5901, options);
            Assert.AreEqual(ExternalVncViewers.UltraVncPath, start.FileName);
            Assert.AreEqual("-viewonly -autoscaling -connect \"desktop.example.invalid::5901\"", start.Arguments);
        }

        [TestMethod]
        public void TigerVnc_UsesViewOnlyWithoutUnsupportedScalingSwitch()
        {
            ExternalVncViewers.Selected = ExternalVncViewers.TigerVnc;
            ExternalVncViewers.TigerVncPath = MakeExecutable("Tiger", "vncviewer64.exe");
            var options = new VncOptions { ViewOnly = true, AutoScale = true };
            var start = VNCConnection.BuildStartInfo("desktop.example.invalid", 5902, options);
            Assert.AreEqual(ExternalVncViewers.TigerVncPath, start.FileName);
            Assert.AreEqual("-ViewOnly \"desktop.example.invalid::5902\"", start.Arguments);
        }

        [TestMethod]
        public void RealVnc_UsesKeyAndPointerSuppression()
        {
            ExternalVncViewers.Selected = ExternalVncViewers.RealVnc;
            ExternalVncViewers.RealVncPath = MakeExecutable("Real", "vncviewer.exe");
            var options = new VncOptions { ViewOnly = true, AutoScale = true };
            var start = VNCConnection.BuildStartInfo("desktop.example.invalid", 5903, options);
            Assert.AreEqual(ExternalVncViewers.RealVncPath, start.FileName);
            Assert.AreEqual("-SendKeyEvents=FALSE -SendPointerEvents=FALSE -Scaling=FitAutoAspect \"desktop.example.invalid::5903\"", start.Arguments);
        }

        [TestMethod]
        public void SelectedVendor_DoesNotFallBackToAnotherViewer()
        {
            ExternalVncViewers.Selected = ExternalVncViewers.UltraVnc;
            ExternalVncViewers.UltraVncPath = Path.Combine(directory, "missing.exe");
            Assert.ThrowsExactly<FileNotFoundException>(() =>
                VNCConnection.BuildStartInfo("desktop.example.invalid", 5900, null));
        }

        private string MakeExecutable(string folder, string filename)
        {
            string path = Path.Combine(directory, folder, filename);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "test viewer");
            return path;
        }

        [TestMethod]
        public void Plugin_ImplementsProtocolAwareDetection()
        {
            var plugin = new Terminals.Connections.VNC.VncConnectionPlugin();
            Assert.IsInstanceOfType(plugin, typeof(IExtraDetection));
        }

        [TestMethod]
        public void Plugin_RecognizesRfbBannerAndRejectsOtherServices()
        {
            AssertBannerDetection("RFB 003.008\n", true);
            AssertBannerDetection("HTTP/1.1 200", false);
        }

        private static void AssertBannerDetection(string banner, bool expected)
        {
            var listener = new System.Net.Sockets.TcpListener(
                System.Net.IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
                var server = System.Threading.Tasks.Task.Run(() =>
                {
                    using (var client = listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    {
                        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(banner);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                });
                var plugin = new Terminals.Connections.VNC.VncConnectionPlugin();
                bool actual = plugin.IsValid("127.0.0.1", port);
                Assert.AreEqual(expected, actual);
                Assert.IsTrue(server.Wait(5000), "Loopback test server did not complete.");
            }
            finally
            {
                listener.Stop();
            }
        }

        [TestMethod]
        public void Connection_IsLaunchOnly()
        {
            var connection = new VNCConnection();
            Assert.IsTrue(connection.LaunchOnly);
            Assert.ThrowsExactly<NotSupportedException>(() => connection.Connect());
            connection.Dispose();
        }
    }
}
