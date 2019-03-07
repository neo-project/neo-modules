using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Settings = Neo.Plugins.Settings;

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
