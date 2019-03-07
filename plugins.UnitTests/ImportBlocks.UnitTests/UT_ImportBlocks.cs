using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Settings = Neo.Plugins.Settings;

namespace ImportBlocksPlugin.UnitTests
{
    [TestClass]
    public class UT_ImportBlocksPlugin
    {
        ImportBlocks uut;

        [TestInitialize]
        public void TestSetup()
        {
            //TODO Mock OnImport() functions
            uut = new ImportBlocks();
        }

        [TestMethod]
        public void TestDefaultConfiguration()
        {
            Settings.Default.MaxOnImportHeight.Should().Be(0u);
        }
   }
}
