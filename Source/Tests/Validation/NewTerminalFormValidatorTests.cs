using System.ComponentModel;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Terminals.Common.Connections;
using Terminals.Forms;
using Tests.Connections;
using Tests.Helpers;

namespace Tests.Validation
{
    [TestClass]
    public class NewTerminalFormValidatorTests
    {
        [TestMethod]
        public void Rdp_EmptyServerName_IsRejected()
        {
            AssertServerNameValidation(string.Empty, true);
        }

        [TestMethod]
        public void Rdp_WhitespaceServerName_IsRejected()
        {
            AssertServerNameValidation("   ", true);
        }

        [TestMethod]
        public void Rdp_PopulatedServerName_IsAccepted()
        {
            AssertServerNameValidation("example.org", false);
        }

        [TestMethod]
        public void Ssh_InactiveUrlValidation_DoesNotRejectFavorite()
        {
            var persistence = TestMocksFactory.CreatePersistence();
            var form = new Mock<INewTerminalForm>();
            form.SetupGet(f => f.ProtocolText).Returns("SSH");
            var sut = new NewTerminalFormValidator(
                persistence.Object, TestConnectionManager.Instance, form.Object);
            using (var control = new TextBox())
            {
                var args = new CancelEventArgs();
                sut.OnUrlValidating(control, args);
                Assert.IsFalse(args.Cancel);
                form.Verify(f => f.GetFullUrlFromHttpTextBox(), Times.Never());
            }
        }

        private static void AssertServerNameValidation(string serverName, bool shouldCancel)
        {
            var persistence = TestMocksFactory.CreatePersistence();
            var form = new Mock<INewTerminalForm>();
            form.SetupGet(f => f.ProtocolText).Returns(KnownConnectionConstants.RDP);
            form.SetupGet(f => f.ServerNameText).Returns(serverName);

            var sut = new NewTerminalFormValidator(
                persistence.Object, TestConnectionManager.Instance, form.Object);
            var control = new TextBox();
            var args = new CancelEventArgs();

            sut.OnServerNameValidating(control, args);

            Assert.AreEqual(shouldCancel, args.Cancel);
            form.Verify(f => f.SetErrorInfo(control, It.IsAny<string>()), Times.Once());
        }
    }
}
