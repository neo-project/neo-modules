using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Multiformats.Address;
using Multiformats.Address.Net;
using Neo.FileStorage.Network;

namespace Neo.FileStorage.Tests.Network
{
    [TestClass]
    public class UT_Address
    {
        [TestMethod]
        public void TestNetAddress()
        {
            string ip = "127.0.0.1";
            int port = 8080;
            var ma = Multiaddress.Decode($"/ip4/{ip}/tcp/{port}");
            var addr = Address.FromString(ma.ToString());
            Assert.AreEqual($"{ip}:{port}", addr.ToIPAddressString());
        }

        [TestMethod]
        public void TestAddress()
        {
            var ip = "127.0.0.1";
            var port = "8080";

            var ma = Multiaddress.Decode(string.Join('/', "/ip4", ip, "tcp", port));

            var addr = Address.FromString(ma.ToString());

            var netAddr = addr.ToIPAddressString();

            Assert.AreEqual(ip + ":" + port, netAddr);
        }

        [TestMethod]
        public void TestHostAddressString()
        {
            Assert.AreEqual("neofs.bigcorp.com:8080", new Address(Multiaddress.Decode("/dns4/neofs.bigcorp.com/tcp/8080")).ToHostAddressString());
            Assert.AreEqual("172.16.14.1:8080", new Address(Multiaddress.Decode("/ip4/172.16.14.1/tcp/8080")).ToHostAddressString());
        }

        [TestMethod]
        public void TestEmpty()
        {
            var address = Address.FromString(":8080");
            Assert.AreEqual("0.0.0.0:8080", address.ToHostAddressString());
            address = Address.FromString(":");
            Assert.AreEqual("0.0.0.0:0", address.ToHostAddressString());
        }

        [TestMethod]
        public void TestEncapsulate()
        {
            var ma1 = "/dns4/neofs.bigcorp.com/tcp/8080";
            var ma2 = "/http";
            var addr1 = new Address(Multiaddress.Decode(ma1));
            var addr2 = new Address(Multiaddress.Decode(ma2));
            var addr = addr1.Encapsulate(addr2);
            Console.WriteLine(addr.ToString());
            Assert.AreEqual(ma1 + ma2, addr.ToString());
            addr = addr2.Encapsulate(addr1);
            Console.WriteLine(addr.ToString());
            Assert.AreEqual(ma2 + ma1, addr.ToString());
        }

        [TestMethod]
        public void TestEquals()
        {
            string addrs = "/ip4/0.0.0.0/tcp/8080";
            Address addr1 = Address.FromString(addrs);
            Assert.IsTrue(addr1.Equals(addr1));
            Address addr2 = Address.FromString(addrs);
            Assert.IsTrue(addr2.Equals(addr1));
            Assert.AreEqual(addr2.GetHashCode(), addr1.GetHashCode());
        }

        [TestMethod]
        public void Intersect()
        {
            string addrs = "/ip4/0.0.0.0/tcp/8080";
            Address addr1 = Address.FromString(addrs);
            Address addr2 = Address.FromString(addrs);
            var l1 = new List<Address>() { addr1 };
            var l2 = new List<Address>() { addr2 };
            Assert.AreEqual(1, l1.Intersect(l2).Count());
        }
    }
}
