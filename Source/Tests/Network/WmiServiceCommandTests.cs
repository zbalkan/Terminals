using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Network.WMI;

namespace Tests.Network
{
    [TestClass]
    public class WmiServiceCommandTests
    {
        [TestMethod]
        public void AcceptedServiceCommand_DoesNotThrow()
        {
            Services.EnsureServiceCommandAccepted("StartService", "ExampleService", 0u);
        }

        [TestMethod]
        public void RejectedServiceCommand_ReportsReturnCodeAndService()
        {
            var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
                Services.EnsureServiceCommandAccepted("StopService", "ExampleService", 3u));
            Assert.IsTrue(error.Message.Contains("ExampleService"));
            Assert.IsTrue(error.Message.Contains("3"));
        }

        [TestMethod]
        public void MissingServiceCommandReturnCode_IsNotAccepted()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                Services.EnsureServiceCommandAccepted("PauseService", "ExampleService", null));
        }
    }
}
