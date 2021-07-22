using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Google.Protobuf;
using Neo.Cryptography.ECC;
using Neo.FileStorage.API.Audit;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.InnerRing.Services.Audit;
using Neo.FileStorage.InnerRing.Utils;
using Neo.FileStorage.InnerRing.Utils.Locode.Db;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Morph.Listen;
using Neo.FileStorage.Reputation;
using Neo.FileStorage.Utils;
using Neo.IO;
using Neo.IO.Data.LevelDB;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using static Neo.FileStorage.InnerRing.Processors.SettlementProcessor;
using static Neo.FileStorage.InnerRing.Timer.Helper;
using static Neo.FileStorage.Morph.Listen.Listener;

namespace Neo.FileStorage.InnerRing
{
    /// <summary>
    /// InneringService is the entry for processing all events related to the inner ring node.
    /// All events will be distributed according to type.(2 event types:MainContractEvent and MorphContractEvent)
    /// Life process:Start--->Assignment event--->Stop
    /// </summary>
    public class InnerRingService : UntypedActor, IState, IDisposable
    {
        private string Name = "InnerRingService";
        //event
        public class ContractEvent { public NotifyEventArgs notify; public bool flag; };
        public class BlockEvent { public Block block; public bool flag; };
        public class Start { };
        public class Stop { };
        // event producers
        private List<BlockTimer> blockTimers = new();
        private BlockTimer epochTimer;
        private IActorRef morphEventListener;
        private IActorRef mainEventListener;
        // global state
        private MainInvoker mainNetClient;
        private MorphInvoker morphClient;
        private long epochCounter;
        private InnerRingIndexer statusIndex;
        private Fixed8ConverterUtil precision;
        private IActorRef auditTaskManager;
        // internal variables
        private readonly Wallet mainWallet;
        private readonly Wallet sideWallet;

        private BalanceContractProcessor balanceContractProcessor;
        private ContainerContractProcessor containerContractProcessor;
        private FsContractProcessor fsContractProcessor;
        private NetMapContractProcessor netMapContractProcessor;
        private GovernanceProcessor governanceProcessor;
        private SettlementProcessor settlementProcessor;
        private AlphabetContractProcessor alphabetContractProcessor;
        private AuditContractProcessor auditContractProcessor;
        private ReputationContractProcessor reputationProcessor;

        private RpcClientCache clientCache;
        private StorageDB _db;

        public InnerRingService(NeoSystem main, NeoSystem side, Wallet pMainwallet = null, Wallet pSideWallet = null, MainInvoker pMainNetClient = null, MorphInvoker pMorphInvokerent = null)
        {
            precision = new Fixed8ConverterUtil();
            // Build 2 client(MorphInvokerentr&MainInvokerent).
            if (pMainwallet is null)
            {
                NEP6Wallet mw = new(Settings.Default.WalletPath, main.Settings);
                mw.Unlock(Settings.Default.Password);
                mainWallet = mw;
            }
            else mainWallet = pMainwallet;
            if (pSideWallet is null)
            {
                NEP6Wallet sw = new(Settings.Default.WalletPath, side.Settings);
                sw.Unlock(Settings.Default.Password);
                sideWallet = sw;
            }
            else sideWallet = pSideWallet;
            if (pMainNetClient is null)
            {
                mainNetClient = new MainInvoker()
                {
                    Wallet = mainWallet,
                    NeoSystem = main,
                    Blockchain = main.Blockchain
                };
            }
            else mainNetClient = pMainNetClient;
            if (pMorphInvokerent is null)
            {
                morphClient = new MorphInvoker()
                {
                    Wallet = sideWallet,
                    NeoSystem = side,
                    Blockchain = side.Blockchain,
                    AlphabetContractHash = Settings.Default.AlphabetContractHash,
                    AuditContractHash = Settings.Default.AuditContractHash,
                    BalanceContractHash = Settings.Default.BalanceContractHash,
                    ContainerContractHash = Settings.Default.ContainerContractHash,
                    FsIdContractHash = Settings.Default.FsIdContractHash,
                    NetMapContractHash = Settings.Default.NetmapContractHash,
                    ReputationContractHash = Settings.Default.ReputationContractHash,
                };
            }
            else morphClient = pMorphInvokerent;
            // Build 2 listeners(MorphEventListener&MainEventListener).
            morphEventListener = side.ActorSystem.ActorOf(Listener.Props("MorphEventListener"));
            mainEventListener = main.ActorSystem.ActorOf(Listener.Props("MainEventListener"));
            // create indexer
            statusIndex = new InnerRingIndexer(morphClient, Settings.Default.IndexerTimeout);
            // create audit processor
            clientCache = new RpcClientCache() { wallet = sideWallet };
            auditTaskManager = side.ActorSystem.ActorOf(Manager.Props(Settings.Default.QueueCapacity,
            side.ActorSystem.ActorOf(WorkerPool.Props("AuditManager", Settings.Default.AuditTaskPoolSize)), () =>
            {
                return side.ActorSystem.ActorOf(WorkerPool.Props("POR", Settings.Default.PorPoolSize));
            }, () =>
            {
                return side.ActorSystem.ActorOf(WorkerPool.Props("PDP", Settings.Default.PdpPoolSize));
            }, clientCache, Settings.Default.MaxPDPSleepInterval));
            auditContractProcessor = new AuditContractProcessor()
            {
                MorphInvoker = morphClient,
                ClientCache = clientCache,
                TaskManager = auditTaskManager,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("AuditContract Processor", Settings.Default.AuditContractWorkersSize)),
            };
            // create settlement processor dependencies
            var auditCalcDeps = new AuditSettlementDeps()
            {
                Invoker = morphClient,
                clientCache = clientCache,
            };
            var basicSettlementDeps = new BasicIncomeSettlementDeps()
            {
                Invoker = morphClient,
            };
            var auditSettlementCalc = new Calculator(auditCalcDeps);
            settlementProcessor = new SettlementProcessor()
            {
                basicIncome = basicSettlementDeps,
                auditProc = auditSettlementCalc,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("Settlement Processor", Settings.Default.SettlementWorkersSize)),
            };
            _db = new("./Data_UNLOCODE");
            var locodeValidator = new Validator(_db);
            // create governance processor
            governanceProcessor = new GovernanceProcessor()
            {
                MainInvoker = mainNetClient,
                MorphInvoker = morphClient,
                ProtocolSettings = main.Settings,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("Governance Processor", Settings.Default.GovernanceWorkersSize)),
            };
            // create netmap processor
            netMapContractProcessor = new NetMapContractProcessor()
            {
                MorphInvoker = morphClient,
                State = this,
                NetmapSnapshot = new CleanupTable(Settings.Default.CleanupEnabled, Settings.Default.CleanupThreshold),
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("NetMapContract Processor", Settings.Default.NetmapContractWorkersSize)),
                HandleNewAudit = OnlyActiveEventHandler(auditContractProcessor.HandleNewAuditRound),
                HandleAuditSettlements = OnlyAlphabetEventHandler(settlementProcessor.HandleAuditEvent),
                HandleAlphabetSync = governanceProcessor.HandleAlphabetSync,
                NodeValidator = locodeValidator
            };
            morphEventListener.Tell(new BindProcessorEvent() { Processor = netMapContractProcessor });
            // create container processor
            containerContractProcessor = new ContainerContractProcessor()
            {
                MorphInvoker = morphClient,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("ContainerContract Processor", Settings.Default.ContainerContractWorkersSize))
            };
            morphEventListener.Tell(new BindProcessorEvent() { Processor = containerContractProcessor });
            // create balance processor
            balanceContractProcessor = new BalanceContractProcessor()
            {
                MainInvoker = mainNetClient,
                Convert = precision,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("BalanceContract Processor", Settings.Default.BalanceContractWorkersSize))
            };
            morphEventListener.Tell(new BindProcessorEvent() { Processor = balanceContractProcessor });
            // create  neofs processor
            fsContractProcessor = new FsContractProcessor()
            {
                MorphInvoker = morphClient,
                Convert = precision,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("FsContract Processor", Settings.Default.FsContractWorkersSize))
            };
            mainEventListener.Tell(new BindProcessorEvent() { Processor = fsContractProcessor });
            // create alphabet processor
            alphabetContractProcessor = new AlphabetContractProcessor()
            {
                MorphInvoker = morphClient,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("AlphabetContract Processor", Settings.Default.AlphabetContractWorkersSize))
            };
            // create reputation processor
            reputationProcessor = new ReputationContractProcessor()
            {
                MorphInvoker = morphClient,
                State = this,
                mngBuilder = new ManagerBuilder() { NetmapSource = morphClient },
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("AlphabetContract Processor", Settings.Default.AlphabetContractWorkersSize))
            };
            morphEventListener.Tell(new BindProcessorEvent() { Processor = fsContractProcessor });
            // todo: create vivid id component
            // initialize epoch timers
            epochTimer = NewEpochTimer(new EpochTimerArgs()
            {
                context = side,
                processor = netMapContractProcessor,
                client = morphClient,
                epoch = this,
                epochDuration = Settings.Default.EpochDuration,
                stopEstimationDMul = Settings.Default.StopEstimationDMul,
                stopEstimationDDiv = Settings.Default.StopEstimationDDiv,
                collectBasicIncome = new SubEpochEventHandler()
                {
                    handler = settlementProcessor.HandleIncomeCollectionEvent,
                    durationMul = Settings.Default.CollectBasicIncomeMul,
                    durationDiv = Settings.Default.CollectBasicIncomeDiv,
                },
                distributeBasicIncome = new SubEpochEventHandler()
                {
                    handler = settlementProcessor.HandleIncomeDistributionEvent,
                    durationMul = Settings.Default.DistributeBasicIncomeMul,
                    durationDiv = Settings.Default.DistributeBasicIncomeDiv,
                }
            });
            blockTimers.Add(epochTimer);
            // initialize emission timer
            var emissionTimer = NewEmissionTimer(new EmitTimerArgs()
            {
                context = side,
                processor = alphabetContractProcessor,
                epochDuration = Settings.Default.AlphabetDuration
            });
            blockTimers.Add(emissionTimer);
        }

        public void InitConfigFromBlockchain()
        {
            ulong epoch;
            try
            {
                epoch = morphClient.Epoch();
            }
            catch (Exception e)
            {
                throw new Exception("can't read epoch" + e.Message);
            }
            uint BalanceDecimals;
            try
            {
                BalanceDecimals = morphClient.BalanceDecimals();
            }
            catch
            {
                throw new Exception("can't read balance contract precision");
            }
            SetEpochCounter((ulong)epoch);
            precision.SetBalanceDecimals(BalanceDecimals);
            Utility.Log("InnerRingService", LogLevel.Info, $"read config from blockchain, active={IsActive()}, epoch={epoch}, precision={BalanceDecimals}");
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Start _:
                    OnStart();
                    break;
                case Stop _:
                    OnStop();
                    break;
                case BlockEvent blockEvent:
                    OnBlockEvent(blockEvent.block, blockEvent.flag);
                    break;
                case ContractEvent contractEvent:
                    OnContractEvent(contractEvent.notify, contractEvent.flag);
                    break;
                default:
                    break;
            }
        }

        private void OnStart()
        {
            try
            {
                InitConfigFromBlockchain();
                VoteForSidechainValidator(Settings.Default.validators);
                morphEventListener.Tell(new BindBlockHandlerEvent()
                {
                    handler = (Block b) =>
                    {
                        Utility.Log("MorphEventListener", LogLevel.Debug, $"new block,index:{b.Index}");
                        TickTimers();
                    }
                });
                morphEventListener.Tell(new Listener.Start());
                mainEventListener.Tell(new Listener.Start());
                StartBlockTimers();
            }
            catch (Exception e)
            {
                Utility.Log("InnerRingService", LogLevel.Error, e.Message);
                return;
            }
        }

        private void StartBlockTimers()
        {
            foreach (var blockTimer in blockTimers)
            {
                blockTimer.Reset();
            }
        }

        private void TickTimers()
        {
            foreach (var blockTimer in blockTimers)
                blockTimer.Tick();
        }

        private void OnStop()
        {
            morphEventListener.Tell(new Listener.Stop());
            mainEventListener.Tell(new Listener.Stop());
            Dispose();
        }

        private void OnContractEvent(NotifyEventArgs notify, bool flag)
        {
            if (flag) mainEventListener.Tell(new NewContractEvent() { notify = notify });
            else morphEventListener.Tell(new NewContractEvent() { notify = notify });
        }

        private void OnBlockEvent(Block block, bool flag)
        {
            if (flag) mainEventListener.Tell(new NewBlockEvent() { block = block });
            else morphEventListener.Tell(new NewBlockEvent() { block = block });
        }

        public ulong EpochCounter()
        {
            return Convert.ToUInt64(epochCounter);
        }

        public void SetEpochCounter(ulong epoch)
        {
            long temp = Convert.ToInt64(epoch);
            Interlocked.Exchange(ref epochCounter, temp);
        }

        public bool IsActive()
        {
            return InnerRingIndex() >= 0;
        }

        public bool IsAlphabet()
        {
            return AlphabetIndex() >= 0;
        }

        public int AlphabetIndex()
        {
            return statusIndex.AlphabetIndex();
        }

        public int InnerRingIndex()
        {
            return statusIndex.InnerRingIndex();
        }

        public int InnerRingSize()
        {
            return statusIndex.InnerRingSize();
        }

        public void VoteForSidechainValidator(ECPoint[] validators)
        {
            Array.Sort(validators);
            var index = InnerRingIndex();
            if (index < 0 || index >= Settings.Default.AlphabetContractHash.Length)
            {
                Utility.Log(Name, LogLevel.Info, "ignore validator vote: node not in alphabet range");
                return;
            }
            if (!validators.Any())
            {
                Utility.Log(Name, LogLevel.Info, "ignore validator vote: empty validators list");
                return;
            }
            var epoch = EpochCounter();
            for (int i = 0; i < Settings.Default.AlphabetContractHash.Length; i++)
            {
                try
                {
                    morphClient.AlphabetVote(i, epoch, validators);
                }
                catch
                {
                    Utility.Log(Name, LogLevel.Info, $"can't invoke vote method in alphabet contract, alphabet_index={i}, epoch={epoch}");
                }
            }
        }

        public void InitAndVoteForSidechainValidator(ECPoint[] validators)
        {
            InitConfigFromBlockchain();
            VoteForSidechainValidator(validators);
        }

        public void WriteReport(Report r)
        {
            IEnumerable<Wallets.WalletAccount> accounts = sideWallet.GetAccounts();
            DataAuditResult res = r.Result();
            res.PublicKey = ByteString.CopyFrom(accounts.ToArray()[0].GetKey().PublicKey.ToArray());
            morphClient.PutAuditResult(res.ToByteArray());
        }

        public void ResetEpochTimer()
        {
            epochTimer.Reset();
        }

        public Action<Morph.Event.ContractEvent> OnlyActiveEventHandler(Action<Morph.Event.ContractEvent> f)
        {
            return (Morph.Event.ContractEvent morphEvent) => { if (IsActive()) f(morphEvent); };
        }
        public Action<Morph.Event.ContractEvent> OnlyAlphabetEventHandler(Action<Morph.Event.ContractEvent> f)
        {
            return (Morph.Event.ContractEvent morphEvent) => { if (IsAlphabet()) f(morphEvent); };
        }

        public void Dispose()
        {
            clientCache?.Dispose();
            auditContractProcessor?.prevAuditCanceler.Cancel();
            auditContractProcessor?.prevAuditCanceler.Dispose();
        }

        public static Props Props(NeoSystem main, NeoSystem side, Wallet pMainWallet = null, Wallet pSideWallet = null, MainInvoker pMainNetClient = null, MorphInvoker pMorphInvokerent = null)
        {
            return Akka.Actor.Props.Create(() => new InnerRingService(main, side, pMainWallet, pSideWallet, pMainNetClient, pMorphInvokerent));
        }
    }
}
