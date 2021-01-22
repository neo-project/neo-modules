using Microsoft.VisualStudio.TestTools.UnitTesting;
using Akka.TestKit.Xunit2;
using Neo.Network.P2P.Payloads;
using Neo.Ledger;
using Neo.SmartContract.Native;
using Neo.Cryptography.ECC;
using Neo.SmartContract;

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
        public void TestFilter()
        {
            var json = @"{
  'Stores': [
    'Lambton Quay',
    'Willis Street'
  ],
  'Manufacturers': [
    {
      'Name': 'Acme Co',
      'Products': [
        {
          'Name': 'Anvil',
          'Price': 50
        }
      ]
    },
    {
      'Name': 'Contoso',
      'Products': [
        {
          'Name': 'Elbow Grease',
          'Price': 99.95
        },
        {
          'Name': 'Headlight Fluid',
          'Price': 4
        }
      ]
    }
  ]
}";

            Assert.AreEqual(@"[""Acme Co""]", Utility.StrictUTF8.GetString(OracleService.Filter(json, "Manufacturers[0].Name")));
            Assert.AreEqual("[50]", Utility.StrictUTF8.GetString(OracleService.Filter(json, "Manufacturers[0].Products[0].Price")));
            Assert.AreEqual(@"[""Elbow Grease""]", Utility.StrictUTF8.GetString(OracleService.Filter(json, "Manufacturers[1].Products[0].Name")));
            Assert.AreEqual(@"[{""Name"":""Elbow Grease"",""Price"":99.95}]", Utility.StrictUTF8.GetString(OracleService.Filter(json, "Manufacturers[1].Products[0]")));
        }

        [TestMethod]
        public void TestCreateOracleResponseTx()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();

            var executionFactor = NativeContract.Policy.GetExecFeeFactor(snapshot);
            Assert.AreEqual(executionFactor, (uint)30);
            var feePerByte = NativeContract.Policy.GetFeePerByte(snapshot);
            Assert.AreEqual(feePerByte, (uint)1000);

            OracleRequest request = new OracleRequest
            {
                OriginalTxid = UInt256.Zero,
                GasForResponse = 100000000 * 1,
                Url = "https://127.0.0.1/test",
                Filter = "",
                CallbackContract = UInt160.Zero,
                CallbackMethod = "callback",
                UserData = System.Array.Empty<byte>()
            };
            byte Prefix_Transaction = 11;
            snapshot.Add(NativeContract.Ledger.CreateStorageKey(Prefix_Transaction, request.OriginalTxid), new StorageItem(new TransactionState()
            {
                BlockIndex = 1,
                Transaction = new Transaction()
                {
                    ValidUntilBlock = 1
                }
            }));
            OracleResponse response = new OracleResponse() { Id = 1, Code = OracleResponseCode.Success, Result = new byte[] { 0x00 } };
            ECPoint[] oracleNodes = new ECPoint[] { ECCurve.Secp256r1.G };
            var tx = OracleService.CreateResponseTx(snapshot, request, response, oracleNodes);

            Assert.AreEqual(167, tx.Size);
            Assert.AreEqual(2216640, tx.NetworkFee);
            Assert.AreEqual(97783360, tx.SystemFee);

            // case (2) The size of attribute exceed the maximum limit

            request.GasForResponse = 0_10000000;
            response.Result = new byte[10250];
            tx = OracleService.CreateResponseTx(snapshot, request, response, oracleNodes);
            Assert.AreEqual(166, tx.Size);
            Assert.AreEqual(OracleResponseCode.InsufficientFunds, response.Code);
            Assert.AreEqual(2215640, tx.NetworkFee);
            Assert.AreEqual(7784360, tx.SystemFee);
        }
    }
}
