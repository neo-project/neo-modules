using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Tests.InnerRing.Processors;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Native;
using Neo.Wallets;

namespace Neo.FileStorage.Tests.Morph.Invoker
{
    [TestClass]
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
            system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()));
            client = new MorphClient()
            {
                wallet = wallet,
                system = system,
                actor=this.TestActor
            };
        }

        [TestMethod]
        public void InvokeLocalFunctionTest()
        {
            InvokeResult result = client.TestInvoke(NativeContract.GAS.Hash, "balanceOf", UInt160.Zero);
            Assert.AreEqual(result.State, VM.VMState.HALT);
            Assert.AreEqual(result.GasConsumed, 2028330);
            Assert.AreEqual(result.ResultStack[0].GetInteger(), 0);
        }

        [TestMethod]
        public void InvokeFunctionTest()
        {
            client.Invoke(out _, NativeContract.GAS.Hash, "balanceOf", 0, UInt160.Zero);
            var result = ExpectMsg<Transaction>();
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void TransferGasTest()
        {
            client.TransferGas(UInt160.Zero, 0);
            var result = ExpectMsg<Transaction>();
            Assert.IsNotNull(result);
        }
    }
}
