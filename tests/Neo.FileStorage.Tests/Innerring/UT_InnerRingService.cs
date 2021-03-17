using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.FileStorage.InnerRing;
using Neo.FileStorage.Morph.Invoker;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets.NEP6;
using System;
using System.Linq;
using static Neo.FileStorage.InnerRing.InnerRingService;

namespace Neo.FileStorage.Tests.InnerRing
{
    [TestClass]
    public class UT_InnerRingService : TestKit
    {
        private NeoSystem system;
        private NEP6Wallet wallet;
        private IActorRef innerring;
        private IClient client;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            client = new MorphClient()
            {
                Wallet = wallet,
                Blockchain = TestActor
            };
            innerring = system.ActorSystem.ActorOf(InnerRingService.Props(system, wallet, client, client));
        }

        [TestMethod]
        public void InitConfigAndContractEventTest()
        {
            innerring.Tell(new InnerRingService.Start());
            var tx = ExpectMsg<Transaction>();
            Assert.IsNotNull(tx);
            //create notify
            tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 0,
                Nonce = 0,
                Script = new byte[] { 0x01 },
                Signers = new Signer[] { new Signer() { Account = wallet.GetAccounts().ToArray()[0].ScriptHash } },
                SystemFee = 0,
                ValidUntilBlock = 0,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            wallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            JArray obj = new JArray();
            obj.Add(tx.ToArray().ToHexString());
            obj.Add(UInt160.Zero.ToArray().ToHexString());
            obj.Add("test");
            obj.Add(new JArray(new VM.Types.Boolean(true).ToJson()));
            NotifyEventArgs notify = FSNode.GetNotifyEventArgsFromJson(obj);
            innerring.Tell(new MainContractEvent() { notify = notify });
            ExpectNoMsg();
            innerring.Tell(new MorphContractEvent() { notify = notify });
            ExpectNoMsg();
            innerring.Tell(new InnerRingService.Stop());
        }
    }
}
