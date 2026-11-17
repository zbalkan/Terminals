using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Forms.Controls;

namespace Tests.UserInterface
{
    [TestClass]
    public class IPAddressControlDesignerTests
    {
        [TestMethod]
        public void Constructor_CreatesFourFieldsAndThreeSeparators()
        {
            using (var control = new IPAddressControl())
            {
                Assert.AreEqual(7, control.Controls.Count);
                CollectionAssert.AreEqual(
                    new[] { "FieldControl0", "DotControl0", "FieldControl1", "DotControl1",
                            "FieldControl2", "DotControl2", "FieldControl3" },
                    control.Controls.Cast<System.Windows.Forms.Control>()
                        .Select(child => child.Name).ToArray());
            }
        }

        [TestMethod]
        public void Text_RoundTripsAcrossDesignerOwnedFields()
        {
            using (var control = new IPAddressControl())
            {
                control.Text = "192.168.1.42";
                Assert.AreEqual("192.168.1.42", control.Text);
                CollectionAssert.AreEqual(new byte[] { 192, 168, 1, 42 },
                    control.GetAddressBytes());
            }
        }
    }
}
