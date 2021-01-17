using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Neo.Plugins.RpcServer.Tests
{
    [TestClass]
    public class UT_RpcServer
    {
        [TestMethod]
        public void TestRpcServerSettingsConstructorForFormatException()
        {
            Action act = () => new RpcServerSettings(maxGasInvoke: 123456m);
            act.Should().NotThrow<FormatException>();
        }
    }
}
