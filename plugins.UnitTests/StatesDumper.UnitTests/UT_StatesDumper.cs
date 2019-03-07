using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Settings = Neo.Plugins.Settings;


namespace StatesDumperPlugin.UnitTests
{
    [TestClass]
    public class UT_StatesDumperPlugin
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
            Settings.Default.PersistAction.Should().Be(PersistActions.StorageChanges);
            Settings.Default.BlockCacheSize.Should().Be(1000);
            Settings.Default.HeightToBegin.Should().Be(0);
            Settings.Default.HeightToStartRealTimeSyncing.Should().Be(2883000);
        }
   }
}
