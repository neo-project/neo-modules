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

namespace StatesDumper.UnitTests
{
    [TestClass]
    public class UT_StatesDumper
    {
        StatesDumper uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new StatesDumper();
        }

        [TestMethod]
        public void TestDefaultConfiguration()
        {
            Settings.Default.PersistAction.Should().Be("StorageChanges");
            Settings.Default.BlockCacheSize.Should().Be(1000);
            Settings.Default.HeightToBegin.Should().Be(0);
            Settings.Default.HeightToRealTimeSyncing.Should().Be(2883000);
        }
   }
}
