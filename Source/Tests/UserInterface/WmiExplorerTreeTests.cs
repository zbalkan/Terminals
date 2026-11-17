using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;
using WMITestClient;

namespace Tests.UserInterface
{
    [TestClass]
    public class WmiExplorerTreeTests
    {
        [TestMethod]
        public void BasicTreeReload_ReplacesExistingNodes()
        {
            using (var control = new WMIControl())
            {
                Control[] matches = control.Controls.Find("treeView2", true);
                Assert.AreEqual(1, matches.Length);
                var tree = (TreeView)matches[0];

                control.LoadBasicTree("<tree Text='First' Type='RootNode'><node Text='Child' Type='ClassNode' /></tree>");
                Assert.AreEqual(1, tree.Nodes.Count);
                Assert.AreEqual("First", tree.Nodes[0].Text);
                Assert.AreEqual(1, tree.Nodes[0].Nodes.Count);
                Assert.AreEqual("Child", tree.Nodes[0].Nodes[0].Text);

                control.LoadBasicTree("<tree Text='Second' Type='RootNode' />");
                Assert.AreEqual(1, tree.Nodes.Count);
                Assert.AreEqual("Second", tree.Nodes[0].Text);
                Assert.AreEqual(0, tree.Nodes[0].Nodes.Count);
            }
        }
    }
}
