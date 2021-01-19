using Microsoft.VisualStudio.TestTools.UnitTesting;
using Multiformats.Address;
using Neo.FSNode.Network;

namespace Neo.FSNode.Tests
{
    [TestClass]
    public class UT_Address
    {
        [TestMethod]
        public void TestAddress()
        {
            var ip = "127.0.0.1";
            var port = "8080";

            var ma = Multiaddress.Decode(string.Join('/', "/ip4", ip, "tcp", port));

            var addr = Address.AddressFromString(ma.ToString());

            var netAddr = addr.IPAddressString();

            Assert.AreEqual(ip + ":" + port, netAddr);
        }
    }
}
