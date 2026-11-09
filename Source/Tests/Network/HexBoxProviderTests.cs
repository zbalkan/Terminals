using Be.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Network
{
    [TestClass]
    public class HexBoxProviderTests
    {
        [TestMethod]
        public void DynamicByteProviderPreservesCapturedBytes()
        {
            byte[] bytes = { 0x00, 0x7F, 0x80, 0xFF };
            var provider = new DynamicByteProvider(bytes);

            Assert.AreEqual((long)bytes.Length, provider.Length);
            for (int index = 0; index < bytes.Length; index++)
                Assert.AreEqual(bytes[index], provider.ReadByte(index));
        }

        [TestMethod]
        public void DynamicByteProviderDoesNotMutateInputBytesWhenEdited()
        {
            byte[] input = { 0x01, 0x02 };
            var provider = new DynamicByteProvider(input);
            provider.WriteByte(0, 0xFE);

            Assert.AreEqual((byte)0xFE, provider.ReadByte(0));
            Assert.AreEqual((byte)0x01, input[0]);
        }
    }
}
