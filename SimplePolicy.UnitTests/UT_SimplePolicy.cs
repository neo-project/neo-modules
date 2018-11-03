using Microsoft.Extensions.Configuration;
using System.IO;
using System.Reflection;
using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;


namespace SimplePolicy.UnitTests
{
    [TestClass]
    public class UT_SimplePolicy
    {
        SimplePolicyPlugin uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new SimplePolicyPlugin();
        }

        [TestMethod]
        public void TestMaxTransactionsPerBlock()
        {
            SimplePolicyPlugin.GetMaxTransactionsPerBlock().Should().Be(500);
            SimplePolicyPlugin.GetMaxFreeTransactionsPerBlock().Should().Be(20);
        }
    }
}
