using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Neo.SmartContract;
using System.IO;

namespace SmartContract.UnitTests
{
    [TestClass]
    public class UT_SmartContract
    {
        internal class TestBox : SmartContractPlugin
        {
            public bool Do(object obj)
            {
                return OnMessage(obj);
            }
        }

        TestBox uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new TestBox();
        }

        [TestMethod]
        public void TestHelloWorld()
        {
            uut.Do(new string[] { "compile", "HelloWorld.dll", "--compatible" }).Should().BeTrue();
            File.ReadAllBytes("HelloWorld.avm").ToScriptHash().ToString().Should().Be("0xe35816a2b6f823a28aa6674ca56c28862fe419f8");
        }

        [TestMethod]
        public void TestAgencyTransaction()
        {
            uut.Do(new string[] { "compile", "AgencyTransaction.dll", "--compatible" }).Should().BeTrue();
            File.ReadAllBytes("AgencyTransaction.avm").ToScriptHash().ToString().Should().Be("0xaed01ea9346ff1db2ead3e82686759d492c53be2");
        }

        [TestMethod]
        public void TestDomain()
        {
            uut.Do(new string[] { "compile", "Domain.dll", "--compatible" }).Should().BeTrue();
            File.ReadAllBytes("Domain.avm").ToScriptHash().ToString().Should().Be("0xbcd12937c4e7e69748769ce6c5b0d6839c3838e3");
        }

        [TestMethod]
        public void TestEventExample()
        {
            uut.Do(new string[] { "compile", "EventExample.dll", "--compatible" }).Should().BeTrue();
            File.ReadAllBytes("EventExample.avm").ToScriptHash().ToString().Should().Be("0xc0b0f7a278cb80db3b0c61243ce7881f6825d9c3");
        }

        [TestMethod]
        public void TestICOTemplate()
        {
            uut.Do(new string[] { "compile", "ICO_Template.dll", "--compatible" }).Should().BeTrue();
            File.ReadAllBytes("ICO_Template.avm").ToScriptHash().ToString().Should().Be("0x3b1c0718473a48cc1d4e1046ffb62c03192dd2b8");
        }

        [TestMethod]
        public void TestLock()
        {
            uut.Do(new string[] { "compile", "Lock.dll", "--compatible" }).Should().BeTrue();
            File.ReadAllBytes("Lock.avm").ToScriptHash().ToString().Should().Be("0x9aaaf6c72672024ad4f312e9484f941f4bcad139");
        }

        [TestMethod]
        public void TestMapExample()
        {
            uut.Do(new string[] { "compile", "MapExample.dll", "--compatible" }).Should().BeTrue();
            File.ReadAllBytes("MapExample.avm").ToScriptHash().ToString().Should().Be("0x4707bc0b203db300ff9694875c539e8b3cb2b313");
        }

        [TestMethod]
        public void TestNEP5()
        {
            uut.Do(new string[] { "compile", "NEP5.dll", "--compatible" }).Should().BeTrue();
            File.ReadAllBytes("NEP5.avm").ToScriptHash().ToString().Should().Be("0x65ca53abaaa7518d041b0f3b92eaa5dda63c9ee2");
        }

        [TestMethod]
        public void TestStructExample()
        {
            uut.Do(new string[] { "compile", "StructExample.dll", "--compatible" }).Should().BeTrue();
            File.ReadAllBytes("StructExample.avm").ToScriptHash().ToString().Should().Be("0xeac817fc264cd4edfebce6947777267666cc5bf5");
        }
    }
}
