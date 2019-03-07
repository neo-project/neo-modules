using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Neo.Network.P2P.Payloads;
using Neo;
using Neo.Persistence;
using Settings = Neo.Plugins.Settings;
using System.Collections.Generic;
using Neo.Cryptography;
using System.Numerics;
using System.Collections;
using System.Linq;
using System;
using Moq;

namespace RpcSecurity.UnitTests
{
    [TestClass]
    public class UT_RpcSecurity
    {
        RpcSecurity uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new RpcSecurity();
        }

        [TestMethod]
        public void TestMaxTransactionsPerBlock()
        {
            Settings.Default.RpcUser.Should().Be("");
            Settings.Default.RpcPass.Should().Be("");
	    string[] DisabledMethodsEmpty = new string[0];
            Settings.Default.DisabledMethods.Should().Be(DisabledMethodsEmpty);
        }
   }
}
