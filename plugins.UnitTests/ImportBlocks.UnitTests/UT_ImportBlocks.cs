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

namespace ImportBlocks.UnitTests
{
    [TestClass]
    public class UT_ImportBlocks
    {
        ImportBlocks uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new ImportBlocks();
        }

        [TestMethod]
        public void TestDefaultConfiguration()
        {
            Settings.Default.MaxOnImportHeight.Should().Be(0);
        }
   }
}
