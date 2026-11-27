using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Terminals;
using Terminals.Data;

namespace Tests.UserInterface
{
    [TestClass]
    public class ShutdownLifecycleTests
    {
        [TestMethod]
        public void OpenTabs_EmptyAndAttached_ReturnExpectedNames()
        {
            using (var tabs = new TabControl.TabControl())
            using (var item = new TerminalTabControlItem("Attached"))
            {
                var controller = CreateController(tabs);
                Assert.IsFalse(controller.HasOpenTabs);
                CollectionAssert.AreEqual(new string[0], controller.GetOpenTabNames());

                tabs.AddTab(item);
                Assert.IsTrue(controller.HasOpenTabs);
                CollectionAssert.AreEqual(new[] { "Attached" }, controller.GetOpenTabNames());
            }
        }

        [TestMethod]
        public void DetachedWindow_CancelledClosing_RetainsRegistrationAndTab()
        {
            using (var tabs = new TabControl.TabControl())
            using (var item = new TerminalTabControlItem("Detached"))
            {
                var controller = CreateController(tabs);
                using (var popup = new PopupTerminal(controller))
                {
                    popup.AddTerminal(item);
                    RegisterDetached(controller, popup);
                    var args = new FormClosingEventArgs(CloseReason.UserClosing, true);
                    RaiseFormEvent(popup, "OnFormClosing", args);

                    Assert.IsTrue(args.Cancel);
                    Assert.IsTrue(controller.HasOpenTabs);
                    CollectionAssert.AreEqual(new[] { "Detached" }, controller.GetOpenTabNames());
                }
            }
        }

        [TestMethod]
        public void DetachedWindow_CommittedClosure_RemovesRegistration()
        {
            using (var tabs = new TabControl.TabControl())
            using (var attached = new TerminalTabControlItem("Attached"))
            using (var detached = new TerminalTabControlItem("Detached"))
            {
                var controller = CreateController(tabs);
                tabs.AddTab(attached);
                using (var popup = new PopupTerminal(controller))
                {
                    popup.AddTerminal(detached);
                    RegisterDetached(controller, popup);
                    CollectionAssert.AreEqual(new[] { "Attached", "Detached" }, controller.GetOpenTabNames());

                    RaiseFormEvent(popup, "OnFormClosed", new FormClosedEventArgs(CloseReason.UserClosing));
                    CollectionAssert.AreEqual(new[] { "Attached" }, controller.GetOpenTabNames());
                }
            }
        }

        private static TerminalTabsSelectionControler CreateController(TabControl.TabControl tabs)
        {
            var persistence = new Mock<IPersistence>();
            persistence.SetupGet(value => value.Dispatcher).Returns(new DataDispatcher());
            return new TerminalTabsSelectionControler(tabs, persistence.Object);
        }

        private static void RegisterDetached(TerminalTabsSelectionControler controller, PopupTerminal popup)
        {
            var field = typeof(TerminalTabsSelectionControler).GetField("detachedWindows",
                BindingFlags.Instance | BindingFlags.NonPublic);
            ((List<PopupTerminal>)field.GetValue(controller)).Add(popup);
        }

        private static void RaiseFormEvent(Form form, string name, EventArgs args)
        {
            typeof(Form).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(form, new object[] { args });
        }
    }
}
