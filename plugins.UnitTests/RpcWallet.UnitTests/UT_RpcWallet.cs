using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;

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
