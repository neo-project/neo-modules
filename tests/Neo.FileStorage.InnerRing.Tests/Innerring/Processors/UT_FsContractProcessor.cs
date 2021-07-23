using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.InnerRing.Utils;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Tests;
using Neo.IO;
using Neo.Wallets;

namespace Neo.FileStorage.InnerRing.Tests.InnerRing.Processors
{
    [TestClass]
    public class UT_FsContractProcessor : TestKit
    {
        private NeoSystem system;
        private FsContractProcessor processor;
        private MorphInvoker morphInvoker;
        private MainInvoker mainInvoker;
        private Wallet wallet;
        private IActorRef actor;
        private TestState state;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            actor = this.ActorOf(Props.Create(() => new ProcessorFakeActor()));
            mainInvoker = TestBlockchain.CreateTestMainInvoker(system, actor, wallet);
            morphInvoker = TestBlockchain.CreateTestMorphInvoker(system, actor, wallet);
            state = new TestState() { alphabetIndex = 1 };
            processor = new FsContractProcessor()
            {
                MorphInvoker = morphInvoker,
                State = state,
                Convert = new Fixed8ConverterUtil(),
                WorkPool = actor
            };
        }

        [TestMethod]
        public void HandleDepositTest()
        {
            processor.HandleDeposit(new DepositEvent()
            {
                Id = new byte[] { 0x01 },
                Amount = 0,
                From = UInt160.Zero,
                To = UInt160.Zero
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleWithdrawTest()
        {
            processor.HandleWithdraw(new WithdrawEvent()
            {
                Id = new byte[] { 0x01 },
                Amount = 0,
                UserAccount = UInt160.Zero
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleChequeTest()
        {
            processor.HandleCheque(new ChequeEvent()
            {
                Id = new byte[] { 0x01 },
                Amount = 0,
                UserAccount = UInt160.Zero,
                LockAccount = UInt160.Zero
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleConfigTest()
        {
            processor.HandleConfig(new ConfigEvent()
            {
                Key = new byte[] { 0x01 },
                Value = new byte[] { 0x01 }
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void ProcessDepositTest()
        {
            state.isAlphabet = true;
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            processor.ProcessDeposit(new DepositEvent()
            {
                Id = new byte[] { 0x01 },
                Amount = 0,
                From = UInt160.Zero,
                To = accounts.ToArray()[0].ScriptHash
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void ProcessWithdrawTest()
        {
            state.isAlphabet = true;
            processor.ProcessWithdraw(new WithdrawEvent()
            {
                Id = UInt160.Zero.ToArray(),
                Amount = 0,
                UserAccount = UInt160.Zero
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void ProcessChequeTest()
        {
            state.isAlphabet = true;
            processor.ProcessCheque(new ChequeEvent()
            {
                Id = new byte[] { 0x01 },
                Amount = 0,
                UserAccount = UInt160.Zero,
                LockAccount = UInt160.Zero
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void ProcessConfigTest()
        {
            state.isAlphabet = true;
            processor.ProcessConfig(new ConfigEvent()
            {
                Id = new byte[] { 0x01 },
                Key = Neo.Utility.StrictUTF8.GetBytes("ContainerFee"),
                Value = new byte[] { 0x01 }
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void ListenerHandlersTest()
        {
            var handlerInfos = processor.ListenerHandlers();
            Assert.AreEqual(handlerInfos.Length, 6);
        }

        [TestMethod]
        public void ListenerParsersTest()
        {
            var parserInfos = processor.ListenerParsers();
            Assert.AreEqual(parserInfos.Length, 6);
        }
    }
}
