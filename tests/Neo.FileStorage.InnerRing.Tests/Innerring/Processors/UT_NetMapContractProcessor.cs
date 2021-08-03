using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.InnerRing.Services.Audit;
using Neo.FileStorage.InnerRing.Timer;
using Neo.FileStorage.InnerRing.Utils.Locode;
using Neo.FileStorage.InnerRing.Utils.Locode.Db;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Listen.Event;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Utils;
using Neo.IO;
using Neo.Wallets;

namespace Neo.FileStorage.InnerRing.Tests.InnerRing.Processors
{
    [TestClass]
    public class UT_NetMapContractProcessor : TestKit
    {
        private NeoSystem system;
        private NetMapContractProcessor processor;
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
            state = new TestState() { alphabetIndex = 1, isAlphabet = true, isActive = true, epoch = 1, actor = this.TestActor };
            var clientCache = new RpcClientCache() { Wallet = wallet };
            var auditTaskManager = system.ActorSystem.ActorOf(Manager.Props(Settings.Default.QueueCapacity,
            system.ActorSystem.ActorOf(WorkerPool.Props("AuditManager", Settings.Default.AuditTaskPoolSize)), () =>
            {
                return system.ActorSystem.ActorOf(WorkerPool.Props("POR", Settings.Default.PorPoolSize));
            }, () =>
            {
                return system.ActorSystem.ActorOf(WorkerPool.Props("PDP", Settings.Default.PdpPoolSize));
            }, clientCache, Settings.Default.MaxPDPSleepInterval));
            var auditContractProcessor = new AuditContractProcessor()
            {
                MorphInvoker = morphInvoker,
                ClientCache = clientCache,
                TaskManager = auditTaskManager,
                State = state,
                WorkPool = actor,
            };
            var governanceProcessor = new GovernanceProcessor()
            {
                MorphInvoker = morphInvoker,
                MainInvoker = mainInvoker,
                ProtocolSettings = system.Settings,
                State = state,
                WorkPool = actor
            };
            var auditCalcDeps = new AuditSettlementDeps()
            {
                Invoker = morphInvoker,
                ClientCache = clientCache,
            };
            var basicSettlementDeps = new BasicIncomeSettlementDeps()
            {
                Invoker = morphInvoker,
            };
            var auditSettlementCalc = new Calculator(auditCalcDeps);
            var settlementProcessor = new SettlementProcessor()
            {
                BasicIncome = basicSettlementDeps,
                AuditProc = auditSettlementCalc,
                State = state,
                WorkPool = actor
            };
            processor = new NetMapContractProcessor()
            {
                MorphInvoker = morphInvoker,
                State = state,
                NetmapSnapshot = new CleanupTable(Settings.Default.CleanupEnabled, Settings.Default.CleanupThreshold),
                WorkPool = actor,
                HandleNewAudit = OnlyActiveEventHandler(auditContractProcessor.HandleNewAuditRound),
                HandleAuditSettlements = OnlyAlphabetEventHandler(settlementProcessor.HandleAuditEvent),
                HandleAlphabetSync = governanceProcessor.HandleAlphabetSync,
            };
        }

        public Action<ContractEvent> OnlyActiveEventHandler(Action<ContractEvent> f)
        {
            return (ContractEvent morphEvent) => { if (state.IsActive()) f(morphEvent); };
        }

        public Action<ContractEvent> OnlyAlphabetEventHandler(Action<ContractEvent> f)
        {
            return (ContractEvent morphEvent) => { if (state.IsAlphabet()) f(morphEvent); };
        }

        [TestMethod]
        public void HandleNewEpochTickTest()
        {
            processor.HandleNewEpochTick();
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleNewEpochTest()
        {
            StorageDB targetDb = new("./Config/Data_LOCODE");
            var locodeValidator = new LocodeValidator(targetDb);
            processor.NodeValidator = locodeValidator;
            processor.HandleNewEpoch(new NewEpochEvent());
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleAddPeerTest()
        {
            processor.HandleAddPeer(new AddPeerEvent());
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleUpdateStateTest()
        {
            IEnumerator<WalletAccount> accounts = wallet.GetAccounts().GetEnumerator();
            accounts.MoveNext();
            processor.HandleUpdateState(new UpdatePeerEvent()
            {
                PublicKey = accounts.Current.GetKey().PublicKey,
                Status = 2
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleCleanupTickTest()
        {
            processor.HandleCleanupTick(new NetmapCleanupTickEvent());
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void ListenerHandlersTest()
        {
            var handlerInfos = processor.ListenerHandlers();
            Assert.AreEqual(handlerInfos.Length, 3);
        }

        [TestMethod]
        public void ListenerParsersTest()
        {
            var parserInfos = processor.ListenerParsers();
            Assert.AreEqual(parserInfos.Length, 3);
        }

        [TestMethod]
        public void ProcessNewEpochTest()
        {
            processor.ProcessNewEpoch(new NewEpochEvent()
            {
                EpochNumber = 2
            });
        }

        [TestMethod]
        public void ProcessNewEpochTickTest()
        {
            processor.ProcessNewEpochTick();
        }

        [TestMethod]
        public void ProcessAddPeerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            var nodeInfo = new API.Netmap.NodeInfo()
            {
                PublicKey = ByteString.CopyFrom(key.PublicKey.ToArray()),
                State = API.Netmap.NodeInfo.Types.State.Online
            };
            processor.ProcessAddPeer(new AddPeerEvent()
            {
                Node = nodeInfo.ToByteArray()
            });
        }

        [TestMethod]
        public void ProcessUpdateStateTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            var nodeInfo = new API.Netmap.NodeInfo()
            {
                PublicKey = ByteString.CopyFrom(key.PublicKey.ToArray()),
                State = API.Netmap.NodeInfo.Types.State.Online
            };
            processor.ProcessUpdateState(new UpdatePeerEvent()
            {
                PublicKey = key.PublicKey,
                Status = (int)API.Netmap.NodeInfo.Types.State.Offline
            });
        }

        [TestMethod]
        public void ProcessNetmapCleanupTickTest()
        {
            processor.ProcessNetmapCleanupTick(new NetmapCleanupTickEvent()
            {
                Epoch = 1
            });
        }
    }
}
