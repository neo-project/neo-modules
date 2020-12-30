using Microsoft.VisualStudio.TestTools.UnitTesting;
using Akka.TestKit.Xunit2;
using Neo.Network.P2P.Payloads;
using Neo.Ledger;

namespace Neo.Plugins.Tests
{
    [TestClass]
    public class UT_OracleService : TestKit
    {
        [TestInitialize]
        public void TestSetup()
        {
            TestBlockchain.InitializeMockNeoSystem();
        }

        [TestMethod]
        public void TestCreateOracleResponseTx()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            //OracleResponse response = new OracleResponse() { Id = 0, Code = OracleResponseCode.Success, Result = new byte[] { 0x00 } };
            //var tx = OracleService.CreateResponseTx(snapshot, response);

            // TODO 
        }
    }
}
