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

namespace ApplicationLogs.UnitTests
{
    [TestClass]
    public class UT_ApplicationLogs
    {
        LogReader uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new LogReader();
        }

        [TestMethod]
        public void TestDefaultConfiguration()
        {
            Settings.Default.Path.Should().Be("ApplicationLogs_{0}");
        }
   }
}
