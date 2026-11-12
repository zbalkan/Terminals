using System;
using System.Collections;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unified;

namespace Tests.External
{
    [TestClass]
    public class SerializationCompatibilityTests
    {
        [TestMethod]
        public void XmlByteAndTextSerialization_PreserveUtf8Content()
        {
            const string value = "Connection Ω & <local>";
            string xml = Serialize.SerializeXMLAsString(value);
            byte[] bytes = Serialize.SerializeXMLAsBytes(value);

            Assert.IsNotNull(xml);
            Assert.IsNotNull(bytes);
            Assert.AreEqual(xml, Encoding.UTF8.GetString(bytes));
            Assert.AreEqual(value, Serialize.DeSerializeXML(xml, typeof(string)) as string);
        }

        [TestMethod]
        public void BinaryByteSerialization_PreservesExistingFormatterOutput()
        {
            var value = new ArrayList { "favorite", 1216 };
            byte[] bytes = Serialize.SerializeBinaryAsBytes(value);

            using (MemoryStream stream = Serialize.SerializeBinary(value))
            {
                CollectionAssert.AreEqual(stream.ToArray(), bytes);
            }

            using (var input = new MemoryStream(bytes))
            {
                var result = Serialize.DeSerializeBinary(input) as ArrayList;
                Assert.IsNotNull(result);
                Assert.AreEqual("favorite", result[0]);
                Assert.AreEqual(1216, result[1]);
            }
        }
    }
}
