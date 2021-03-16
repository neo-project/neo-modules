using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Morph.Invoke;
using Neo.FileStorage.InnerRing.Processors;
using Neo.IO;
using Neo.Plugins.util;
using Neo.Wallets;
using System.Collections.Generic;
using System.Linq;
using static Neo.FileStorage.Morph.Event.MorphEvent;

namespace Neo.FileStorage.Tests.InnerRing.Processors
{
    [TestClass]
    public class UT_BalanceContractProcessor : TestKit
    {
        private NeoSystem system;
        private BalanceContractProcessor processor;
        private MorphClient morphclient;
        private Wallet wallet;
        private TestActiveState activeState;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            activeState = new TestActiveState();
            activeState.SetActive(true);
            morphclient = new MorphClient()
            {
                Wallet = wallet,
                Blockchain = system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()))
            };
            processor = new BalanceContractProcessor()
            {
                Client = morphclient,
                Convert = new Fixed8ConverterUtil(),
                ActiveState = activeState,
                WorkPool = system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()))
            };
        }

        [TestMethod]
        public void HandleLockTest()
        {
            processor.HandleLock(new LockEvent()
            {
                Id = new byte[] { 0x01 }
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void ProcessLockTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            processor.ProcessLock(new LockEvent()
            {
                Id = new byte[] { 0x01 },
                Amount = 0,
                LockAccount = accounts.ToArray()[0].ScriptHash,
                UserAccount = accounts.ToArray()[0].ScriptHash
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(tx);
            activeState.SetActive(false);
            processor.ProcessLock(new LockEvent()
            {
                Id = new byte[] { 0x01 },
                Amount = 0,
                LockAccount = accounts.ToArray()[0].ScriptHash,
                UserAccount = accounts.ToArray()[0].ScriptHash
            });
            ExpectNoMsg();
        }

        [TestMethod]
        public void ListenerHandlersTest()
        {
            var handlerInfos = processor.ListenerHandlers();
            Assert.AreEqual(handlerInfos.Length, 1);
        }

        [TestMethod]
        public void ListenerParsersTest()
        {
            var parserInfos = processor.ListenerParsers();
            Assert.AreEqual(parserInfos.Length, 1);
        }

        [TestMethod]
        public void ListenerTimersHandlersTest()
        {
            var handlerInfos = processor.TimersHandlers();
            Assert.AreEqual(0, handlerInfos.Length);
        }
    }
}
