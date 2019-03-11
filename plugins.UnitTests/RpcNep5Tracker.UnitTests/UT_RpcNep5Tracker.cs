using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;

namespace RpcNep5Tracker.UnitTests
{
    [TestClass]
    public class UT_RpcNep5Tracker
    {
        RpcNep5Tracker uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new RpcNep5Tracker();
        }
   }
}
