using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Settings = Neo.Plugins.Settings;
using Neo.Plugins;

namespace RpcSecurityPlugin.UnitTests
{
    [TestClass]
    public class UT_RpcSecurityPlugin
    {
        RpcSecurity uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new RpcSecurity();
        }

        [TestMethod]
        public void TestDefaultConfiguration()
        {
            Settings.Default.RpcUser.Should().Be(null);
            Settings.Default.RpcPass.Should().Be(null);
        }
   }
}
