using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;

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
            uut.Do("compile HelloWorld.dll --compatible").Should().BeTrue();
        }

        [TestMethod]
        public void TestAgencyTransaction()
        {
            uut.Do("compile AgencyTransaction.dll --compatible").Should().BeTrue();
        }

        [TestMethod]
        public void TestDomain()
        {
            uut.Do("compile Domain.dll --compatible").Should().BeTrue();
        }

        [TestMethod]
        public void TestEventExample()
        {
            uut.Do("compile EventExample.dll --compatible").Should().BeTrue();
        }

        [TestMethod]
        public void TestICOTemplate()
        {
            uut.Do("compile ICO_Template.dll --compatible").Should().BeTrue();
        }

        [TestMethod]
        public void TestLock()
        {
            uut.Do("compile Lock.dll --compatible").Should().BeTrue();
        }

        [TestMethod]
        public void TestMapExample()
        {
            uut.Do("compile MapExample.dll --compatible").Should().BeTrue();
        }

        [TestMethod]
        public void TestNEP5()
        {
            uut.Do("compile NEP5.dll --compatible").Should().BeTrue();
        }

        [TestMethod]
        public void TestStructExample()
        {
            uut.Do("compile StructExample.dll --compatible").Should().BeTrue();
        }
    }
}
