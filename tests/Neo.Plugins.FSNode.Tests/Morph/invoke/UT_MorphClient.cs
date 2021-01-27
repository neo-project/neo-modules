using Akka.Actor;
using Akka.TestKit.Xunit2;
using FSStorageTests.innering.processors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins.FSStorage.morph.invoke;
using Neo.SmartContract.Native;
using Neo.Wallets;

namespace Neo.Plugins.FSStorage.morph.client.Tests
{
    [TestClass()]
    public class UT_MorphClient : TestKit
    {
        private NeoSystem system;
        private MorphClient client;
        private Wallet wallet;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            client = new MorphClient()
            {
                Wallet = wallet,
                Blockchain = system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()))
            };
        }

        [TestMethod()]
        public void InvokeLocalFunctionTest()
        {
            InvokeResult result = client.InvokeLocalFunction(NativeContract.GAS.Hash, "balanceOf", UInt160.Zero);
            Assert.AreEqual(result.State, VM.VMState.HALT);
            Assert.AreEqual(result.GasConsumed, 1999390);
            Assert.AreEqual(result.ResultStack[0].GetInteger(), 0);
        }

        [TestMethod()]
        public void InvokeFunctionTest()
        {
            client.InvokeFunction(NativeContract.GAS.Hash, "balanceOf", 0, UInt160.Zero);
            var result = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(result);
        }

        [TestMethod()]
        public void TransferGasTest()
        {
            client.TransferGas(UInt160.Zero, 0);
            var result = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(result);
        }
    }
}
