using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Connections;

namespace Tests.UserInterface
{
    [TestClass]
    public class RdpMenuVisitorTests
    {
        [TestMethod]
        public void EmptyToolBar_UpdateMenu_CreatesNewButtons()
        {
            var sut = new MenuVisitorSut();
            var menuVisitor = new RdpMenuVisitor(sut.MockProvider.Object);
            menuVisitor.Visit(sut.Toolbar);
            // the sub menu is created dynamicaly
            var serveButton = sut.Toolbar.Items[0] as ToolStripDropDownButton;
            Assert.IsNotNull(serveButton, "First visit should update the menu by add its own drop donw menu button");
        }
        [TestMethod]
        public void RefreshMenuItem_AddsLocalizedRefreshCommand()
        {
            var sut = new MenuVisitorSut();
            var connection = new RDPConnection();
            sut.MockProvider.SetupGet(p => p.CurrentConnection).Returns(connection);
            var visitor = new RdpMenuVisitor(sut.MockProvider.Object);
            visitor.Visit(sut.Toolbar);

            new PrivateObject(visitor).Invoke("AddRefreshMenuItem", connection);

            var button = sut.Toolbar.Items[RdpMenuVisitor.TERMINAL_SERVER_MENU_BUTTON_NAME]
                as ToolStripDropDownButton;
            Assert.IsNotNull(button);
            Assert.AreEqual(2, button.DropDownItems.Count);
            var refresh = button.DropDownItems[1] as ToolStripMenuItem;
            Assert.IsNotNull(refresh);
            Assert.AreEqual("Refresh sessions", refresh.Text);
        }

        [TestMethod]
        public void RefreshFromPreviouslyActiveConnection_DoesNotStartDiscovery()
        {
            var sut = new MenuVisitorSut();
            var earlier = new RDPConnection();
            var current = earlier;
            sut.MockProvider.SetupGet(p => p.CurrentConnection).Returns(() => current);
            var visitor = new RdpMenuVisitor(sut.MockProvider.Object);
            visitor.Visit(sut.Toolbar);
            new PrivateObject(visitor).Invoke("AddRefreshMenuItem", earlier);

            var button = sut.Toolbar.Items[RdpMenuVisitor.TERMINAL_SERVER_MENU_BUTTON_NAME]
                as ToolStripDropDownButton;
            Assert.IsNotNull(button);
            var refresh = button.DropDownItems[1] as ToolStripMenuItem;
            Assert.IsNotNull(refresh);

            current = new RDPConnection();
            refresh.PerformClick();
            Assert.IsNull(earlier.TerminalServerLoadTask,
                "Stale refresh must not enumerate sessions on a previous connection.");
            Assert.IsNull(current.TerminalServerLoadTask);
        }

        [TestMethod]
        public void Visit_RdpConnection_DoesNotQueryTerminalServices()
        {
            var sut = new MenuVisitorSut();
            var connection = new RDPConnection();
            sut.MockProvider.SetupGet(p => p.CurrentConnection).Returns(connection);
            var visitor = new RdpMenuVisitor(sut.MockProvider.Object);

            visitor.Visit(sut.Toolbar);

            var button = sut.Toolbar.Items[RdpMenuVisitor.TERMINAL_SERVER_MENU_BUTTON_NAME]
                as ToolStripDropDownButton;
            Assert.IsNotNull(button);
            Assert.IsTrue(button.Available, "The Terminal Server menu should be available for an RDP connection");
            Assert.IsNull(connection.TerminalServerLoadTask);
            Assert.IsTrue(button.ToolTipText.Contains("current Windows account"));
        }

    }
}
