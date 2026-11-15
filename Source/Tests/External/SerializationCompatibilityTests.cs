using System;
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
    }
}
