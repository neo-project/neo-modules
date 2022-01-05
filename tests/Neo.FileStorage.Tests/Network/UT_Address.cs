using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Multiformats.Address;
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
            Assert.AreEqual($"{ip}:{port}", addr.ToHostAddressString());
        }

        [TestMethod]
        public void TestAddress()
        {
            var ip = "127.0.0.1";
            var port = "8080";

            var ma = Multiaddress.Decode(string.Join('/', "/ip4", ip, "tcp", port));

            var addr = Address.FromString(ma.ToString());

            var netAddr = addr.ToHostAddressString();

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
            string addr = "/ip4/0.0.0.0/tcp/8080";
            Address addr1 = Address.FromString(addr);
            Address addr2 = Address.FromString(addr);
            var l1 = new List<Address>() { addr1 };
            var l2 = new List<Address>() { addr2 };
            Assert.AreEqual(1, l1.Intersect(l2).Count());
        }

        [TestMethod]
        public void TestTls()
        {
            var addr = "/dns4/s04.neofs.devenv/tcp/8082/tls";
            Address naddr = Address.FromString(addr);
            Assert.AreEqual(addr, naddr.ToString());
            addr = "/dns4/s04.neofs.devenv/tcp/8082/tls/http";
            naddr = Address.FromString(addr);
            Assert.AreEqual(addr, naddr.ToString());
        }

        [TestMethod]
        public void TestUdp()
        {
            var addr = "/dns4/neofs.bigcorp.com/udp/8080/tcp/8080";
            Address naddr = Address.FromString(addr);
            Console.WriteLine(naddr.ToHostAddressString());
        }

        [TestMethod]
        public void TestToRpcAddressString()
        {
            var addr = "/dns4/s04.neofs.devenv/tcp/8082/tls";
            Address naddr = Address.FromString(addr);
            Assert.AreEqual("https://s04.neofs.devenv:8082", naddr.ToRpcAddressString());
            addr = "/dns4/s04.neofs.devenv/tcp/8082/tls/http";
            naddr = Address.FromString(addr);
            Assert.AreEqual("https://s04.neofs.devenv:8082", naddr.ToRpcAddressString());
        }

        [TestMethod]
        public void TestRpcNotSupported()
        {
            var addr = "/dns4/s04.neofs.devenv/udp/8082";
            Assert.ThrowsException<NotSupportedException>(() => Address.FromString(addr));
        }

        [TestMethod]
        public void TestInvalidAddress()
        {
            Assert.ThrowsException<FormatException>(() => Address.FromString("invalid://example.com:7070"));
            Assert.ThrowsException<FormatException>(() => Address.FromString("wtf://example.com:123"));
            Assert.ThrowsException<FormatException>(() => Address.FromString("grpc://example.com"));
        }
    }
}
