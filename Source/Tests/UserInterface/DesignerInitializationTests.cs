using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using Terminals.Forms.OptionPanels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Forms.Controls;

namespace Tests.UserInterface
{
    [TestClass]
    public class DesignerInitializationTests
    {
        [TestMethod]
        public void CustomIPAddressChildren_RetainStaticProperties()
        {
            using (var dot = new IPAddressDotControl())
            using (var field = new IPAddressFieldControl())
            {
                Assert.AreEqual(".", dot.Text);
                Assert.IsFalse(dot.TabStop);
                Assert.AreEqual(System.Windows.Forms.BorderStyle.None, field.BorderStyle);
                Assert.AreEqual(3, field.MaxLength);
                Assert.IsFalse(field.TabStop);
                Assert.AreEqual(System.Windows.Forms.HorizontalAlignment.Center, field.TextAlign);
            }
        }

        [TestMethod]
        public void FavoritesList_LoadBeforeServices_DoesNotAccessUnassignedDependencies()
        {
            using (var favorites = new TestableFavoritesList())
            {
                favorites.RaiseLoad();
            }
        }

        [TestMethod]
        public void PluginsOptionPanel_DesignTime_DoesNotReadApplicationSettings()
        {
            using (var panel = (PluginsOptionPanel)LicenseManager.CreateWithContext(
                typeof(PluginsOptionPanel), new TestDesignTimeLicenseContext()))
            {
                Assert.AreEqual(1, panel.Controls.Count);
                var groups = panel.Controls[0] as FlowLayoutPanel;
                Assert.IsNotNull(groups);
                Assert.AreEqual(5, groups.Controls.Count);
            }
        }

        [TestMethod]
        public void CredentialsPanel_DesignTime_DoesNotDeserializeImageList()
        {
            using (var panel = (CredentialsPanel)LicenseManager.CreateWithContext(
                typeof(CredentialsPanel), new TestDesignTimeLicenseContext()))
            {
                Assert.IsTrue(panel.Controls.Count > 0);
            }
        }

        [TestMethod]
        public void GeneralPropertiesUserControl_DesignTime_ConstructsNestedSecurityPanel()
        {
            using (var panel = (Terminals.Forms.EditFavorite.GeneralPropertiesUserControl)
                LicenseManager.CreateWithContext(
                    typeof(Terminals.Forms.EditFavorite.GeneralPropertiesUserControl),
                    new TestDesignTimeLicenseContext()))
            {
                Assert.IsTrue(panel.Controls.Count > 0);
            }
        }

        [TestMethod]
        public void FavoritePropertiesControl_DesignTime_ConstructsNestedProperties()
        {
            using (var panel = (Terminals.Forms.EditFavorite.FavoritePropertiesControl)
                LicenseManager.CreateWithContext(
                    typeof(Terminals.Forms.EditFavorite.FavoritePropertiesControl),
                    new TestDesignTimeLicenseContext()))
            {
                Assert.IsTrue(panel.Controls.Count > 0);
            }
        }

        [TestMethod]
        public void FavoritesList_DesignTime_DoesNotInitializeRuntimeServices()
        {
            using (var panel = (Terminals.FavsList)LicenseManager.CreateWithContext(
                typeof(Terminals.FavsList), new TestDesignTimeLicenseContext()))
            {
                Assert.IsTrue(panel.Controls.Count > 0);
            }
        }

        [TestMethod]
        public void CustomContainersAndButtons_ConstructSuccessfully()
        {
            using (var panel = new TabControl.BaseStyledPanel())
            using (var tabs = new TabControl.TabControl())
            using (var item = new TabControl.TabControlItem())
            using (var splitButton = new SplitButton())
            using (var toolStrips = new Terminals.Forms.Controls.ToolStripContainer())
            {
                Assert.IsNotNull(panel);
                Assert.AreEqual(new System.Drawing.Size(350, 200), tabs.Size);
                Assert.IsNotNull(item);
                Assert.IsTrue(splitButton.AutoSize);
                Assert.IsNotNull(toolStrips.TopToolStripPanel);
            }
        }

        private sealed class TestDesignTimeLicenseContext : LicenseContext
        {
            public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } }
            public override string GetSavedLicenseKey(Type type, Assembly resourceAssembly) { return null; }
            public override void SetSavedLicenseKey(Type type, string key) { }
        }

        private sealed class TestableFavoritesList : Terminals.FavsList
        {
            internal void RaiseLoad()
            {
                base.OnLoad(System.EventArgs.Empty);
            }
        }
    }
}
