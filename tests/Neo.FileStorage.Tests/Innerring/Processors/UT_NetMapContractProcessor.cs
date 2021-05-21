using Akka.Actor;
using Akka.TestKit.Xunit2;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.API.Netmap;
using Neo.IO;
using Neo.Wallets;
using static Neo.FileStorage.Morph.Event.MorphEvent;
using static Neo.FileStorage.InnerRing.Timer.TimerTickEvent;
using System.Collections.Generic;
using System.Linq;
using Neo.Plugins.util;
using Neo.FileStorage.Morph.Event;
using System;
using Neo.FileStorage.InnerRing;
using Neo.FileStorage.Services.Audit;
using static Neo.FileStorage.InnerRing.Processors.SettlementProcessor;
using Neo.FileStorage.Utils.Locode.Db;

namespace Neo.FileStorage.Tests.InnerRing.Processors
{
    [TestClass]
    public class UT_NetMapContractProcessor : TestKit
    {
        private NeoSystem system;
        private NetMapContractProcessor processor;
        private Client morphclient;
        private Wallet wallet;
        private IActorRef actor;
        private TestUtils.TestState state;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            actor = this.ActorOf(Props.Create(() => new ProcessorFakeActor()));
            morphclient = new Client()
            {
                client = new MorphClient()
                {
                    wallet = wallet,
                    system = system,
                    actor = actor
                }
            };
            state = new TestUtils.TestState() { alphabetIndex = 1 ,isAlphabet=true,isActive=true,epoch=1};
            var clientCache = new RpcClientCache() { wallet = wallet };
            var auditTaskManager = system.ActorSystem.ActorOf(Manager.Props(Settings.Default.QueueCapacity, clientCache, Settings.Default.MaxPDPSleepInterval));
            var auditContractProcessor = new AuditContractProcessor()
            {
                MorphCli = morphclient,
                ClientCache = clientCache,
                TaskManager = auditTaskManager,
                State = state,
                WorkPool=actor,
            };
            var governanceProcessor = new GovernanceProcessor()
            {
                MorphCli = morphclient,
                MainCli = morphclient,
                ProtocolSettings = system.Settings,
                State = state,
                WorkPool = actor
            };
            var auditCalcDeps = new AuditSettlementDeps()
            {
                client = morphclient,
                clientCache = clientCache,
            };
            var basicSettlementDeps = new BasicIncomeSettlementDeps()
            {
                client = morphclient,
            };
            var auditSettlementCalc = new Calculator(auditCalcDeps);
            var settlementProcessor = new SettlementProcessor()
            {
                basicIncome = basicSettlementDeps,
                auditProc = auditSettlementCalc,
                State = state,
                WorkPool = actor
            };
            StorageDB targetDb = new("./Config/Data_LOCODE/"); 
            var locodeValidator = new Validator(targetDb);
            processor = new NetMapContractProcessor()
            {
                MorphCli = morphclient,
                State = state,
                NetmapSnapshot = new CleanupTable(Settings.Default.CleanupEnabled, Settings.Default.CleanupThreshold),
                WorkPool = actor,
                HandleNewAudit = OnlyActiveEventHandler(auditContractProcessor.HandleNewAuditRound),
                HandleAuditSettlements = OnlyAlphabetEventHandler(settlementProcessor.HandleAuditEvent),
                HandleAlphabetSync = governanceProcessor.HandleAlphabetSync,
                NodeValidator = locodeValidator
            };
        }
        public Action<IContractEvent> OnlyActiveEventHandler(Action<IContractEvent> f)
        {
            return (IContractEvent morphEvent) => { if (state.IsActive()) f(morphEvent); };
        }
        public Action<IContractEvent> OnlyAlphabetEventHandler(Action<IContractEvent> f)
        {
            return (IContractEvent morphEvent) => { if (state.IsAlphabet()) f(morphEvent); };
        }


        [TestMethod]
        public void HandleNewEpochTickTest()
        {
            processor.HandleNewEpochTick(new NewEpochTickEvent());
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleNewEpochTest()
        {
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
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void ProcessNewEpochTickTest()
        {
            processor.ProcessNewEpochTick(new NewEpochTickEvent());
        }

        [TestMethod]
        public void ProcessAddPeerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            var nodeInfo = new API.Netmap.NodeInfo()
            {
                PublicKey = ByteString.CopyFrom(key.PublicKey.ToArray()),
                Address = API.Cryptography.KeyExtension.PublicKeyToAddress(key.PublicKey.ToArray()),
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
                Address = API.Cryptography.KeyExtension.PublicKeyToAddress(key.PublicKey.ToArray()),
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

        public class EpochTimerReseter //: IEpochTimerReseter
        {
            public void ResetEpochTimer()
            {
                return;
            }
        }
    }
}
