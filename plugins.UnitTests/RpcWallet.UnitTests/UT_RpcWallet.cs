using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Neo.Network.P2P.Payloads;
using Neo;
using Neo.Persistence;
using System.Collections.Generic;
using Neo.Cryptography;
using System.Numerics;
using System.Collections;
using System.Linq;
using System;
using Moq;

namespace RpcWalletPlugin.UnitTests
{
    [TestClass]
    public class UT_RpcWalletPlugin
    {
        RpcWallet uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new RpcWallet();
        }
   }
}
