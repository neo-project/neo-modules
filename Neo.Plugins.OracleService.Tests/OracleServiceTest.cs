using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P.Payloads;

namespace Neo.Plugins.OracleService.Tests
{
    [TestClass]
    public class OracleServiceTest
    {
        [TestMethod]
       public void TestCreateOracleRespone()
       {
            OracleResponse respone = new OracleResponse();
            OracleService.CreateResponseTx(snapshot, respone);
       }
    }
}
