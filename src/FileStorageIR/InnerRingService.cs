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
using Neo.FileStorage.InnerRing.Timer;
using Neo.FileStorage.InnerRing.Utils;
using Neo.FileStorage.InnerRing.Utils.Locode;
using Neo.FileStorage.InnerRing.Utils.Locode.Db;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Listen;
using Neo.FileStorage.Reputation;
using Neo.FileStorage.Utils;
using Neo.IO;
using Neo.IO.Data.LevelDB;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using static Neo.FileStorage.InnerRing.Timer.Helper;

namespace Neo.FileStorage.InnerRing
{
    /// <summary>
    /// InneringService is the entry for processing all events related to the inner ring node.
    /// All events will be distributed according to type.(2 event types:MainContractEvent and MorphContractEvent)
    /// Life process:Start--->Assignment event--->Stop
    /// </summary>
    public class InnerRingService : UntypedActor, IState, IDisposable
    {
        public class ContractEvent { public NotifyEventArgs Notify; public bool Flag; };
        public class BlockEvent { public Block Block; public bool Flag; };
        public class Start { };
        public class Stop { };
        private readonly List<BlockTimer> blockTimers = new();
        private readonly BlockTimer epochTimer;
        private readonly IActorRef morphEventListener;
        private readonly IActorRef mainEventListener;
        private readonly MainInvoker mainInvoker;
        private readonly MorphInvoker morphInvoker;
        private long epochCounter;
        private readonly InnerRingIndexer statusIndex;
        private readonly Fixed8ConverterUtil precision;
        private readonly IActorRef auditTaskManager;
        private readonly Wallet mainWallet;
        private readonly Wallet sideWallet;
        private readonly BalanceContractProcessor balanceContractProcessor;
        private readonly ContainerContractProcessor containerContractProcessor;
        private readonly FsContractProcessor fsContractProcessor;
        private readonly NetMapContractProcessor netMapContractProcessor;
        private readonly GovernanceProcessor governanceProcessor;
        private readonly SettlementProcessor settlementProcessor;
        private readonly AlphabetContractProcessor alphabetContractProcessor;
        private readonly AuditContractProcessor auditContractProcessor;
        private readonly ReputationContractProcessor reputationProcessor;
        private readonly RpcClientCache clientCache;
        private readonly StorageDB _db;

        public InnerRingService(NeoSystem main, NeoSystem side, Wallet pMainwallet = null, Wallet pSideWallet = null, MainInvoker pMainNetClient = null, MorphInvoker pMorphInvokerent = null)
        {
            precision = new Fixed8ConverterUtil();
            if (pMainwallet is null)
            {
                NEP6Wallet mw = new(Settings.Default.WalletPath, main.Settings);
                mw.Unlock(Settings.Default.Password);
                mainWallet = mw;
            }
            else
                mainWallet = pMainwallet;
            if (pSideWallet is null)
            {
                NEP6Wallet sw = new(Settings.Default.WalletPath, side.Settings);
                sw.Unlock(Settings.Default.Password);
                sideWallet = sw;
            }
            else
                sideWallet = pSideWallet;
            if (pMainNetClient is null)
            {
                mainInvoker = new MainInvoker()
                {
                    Wallet = mainWallet,
                    NeoSystem = main,
                    Blockchain = main.Blockchain,
                    FsContractHash = Settings.Default.FsContractHash,
                    MainChainFee = Settings.Default.MainChainFee
                };
            }
            else
                mainInvoker = pMainNetClient;
            if (pMorphInvokerent is null)
            {
                morphInvoker = new MorphInvoker()
                {
                    Wallet = sideWallet,
                    NeoSystem = side,
                    Blockchain = side.Blockchain,
                    SideChainFee = Settings.Default.SideChainFee,
                    AlphabetContractHash = Settings.Default.AlphabetContractHash,
                    AuditContractHash = Settings.Default.AuditContractHash,
                    BalanceContractHash = Settings.Default.BalanceContractHash,
                    ContainerContractHash = Settings.Default.ContainerContractHash,
                    FsIdContractHash = Settings.Default.FsIdContractHash,
                    NetMapContractHash = Settings.Default.NetmapContractHash,
                    ReputationContractHash = Settings.Default.ReputationContractHash,
                };
            }
            else
                morphInvoker = pMorphInvokerent;
            morphEventListener = side.ActorSystem.ActorOf(Listener.Props("MorphEventListener"));
            mainEventListener = main.ActorSystem.ActorOf(Listener.Props("MainEventListener"));
            statusIndex = new InnerRingIndexer(morphInvoker, Settings.Default.IndexerTimeout);
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
                MorphInvoker = morphInvoker,
                ClientCache = clientCache,
                TaskManager = auditTaskManager,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("AuditContractProcessor", Settings.Default.AuditContractWorkersSize)),
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
            settlementProcessor = new SettlementProcessor()
            {
                BasicIncome = basicSettlementDeps,
                AuditProc = auditSettlementCalc,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("SettlementProcessor", Settings.Default.SettlementWorkersSize)),
            };
            _db = new("./Data_UNLOCODE");
            var locodeValidator = new LocodeValidator(_db);
            governanceProcessor = new GovernanceProcessor()
            {
                MainInvoker = mainInvoker,
                MorphInvoker = morphInvoker,
                ProtocolSettings = main.Settings,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("GovernanceProcessor", Settings.Default.GovernanceWorkersSize)),
            };
            netMapContractProcessor = new NetMapContractProcessor()
            {
                MorphInvoker = morphInvoker,
                State = this,
                NetmapSnapshot = new CleanupTable(Settings.Default.CleanupEnabled, Settings.Default.CleanupThreshold),
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("NetMapContractProcessor", Settings.Default.NetmapContractWorkersSize)),
                HandleNewAudit = OnlyActiveEventHandler(auditContractProcessor.HandleNewAuditRound),
                HandleAuditSettlements = OnlyAlphabetEventHandler(settlementProcessor.HandleAuditEvent),
                HandleAlphabetSync = governanceProcessor.HandleAlphabetSync,
                NodeValidator = locodeValidator
            };
            morphEventListener.Tell(new Listener.BindProcessorEvent() { Processor = netMapContractProcessor });
            containerContractProcessor = new ContainerContractProcessor()
            {
                MorphInvoker = morphInvoker,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("ContainerContractProcessor", Settings.Default.ContainerContractWorkersSize))
            };
            morphEventListener.Tell(new Listener.BindProcessorEvent() { Processor = containerContractProcessor });
            balanceContractProcessor = new BalanceContractProcessor()
            {
                MainInvoker = mainInvoker,
                Convert = precision,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("BalanceContractProcessor", Settings.Default.BalanceContractWorkersSize))
            };
            morphEventListener.Tell(new Listener.BindProcessorEvent() { Processor = balanceContractProcessor });
            fsContractProcessor = new FsContractProcessor()
            {
                MorphInvoker = morphInvoker,
                Convert = precision,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("FsContractProcessor", Settings.Default.FsContractWorkersSize))
            };
            mainEventListener.Tell(new Listener.BindProcessorEvent() { Processor = fsContractProcessor });
            alphabetContractProcessor = new AlphabetContractProcessor()
            {
                MorphInvoker = morphInvoker,
                State = this,
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("AlphabetContractProcessor", Settings.Default.AlphabetContractWorkersSize))
            };
            reputationProcessor = new ReputationContractProcessor()
            {
                MorphInvoker = morphInvoker,
                State = this,
                ManagerBuilder = new ManagerBuilder() { NetmapSource = morphInvoker },
                WorkPool = side.ActorSystem.ActorOf(WorkerPool.Props("AlphabetContractProcessor", Settings.Default.AlphabetContractWorkersSize))
            };
            morphEventListener.Tell(new Listener.BindProcessorEvent() { Processor = fsContractProcessor });
            epochTimer = NewEpochTimer(new EpochTimerArgs()
            {
                Processor = netMapContractProcessor,
                MorphInvoker = morphInvoker,
                EpochState = this,
                EpochDuration = Settings.Default.EpochDuration,
                StopEstimationDMul = Settings.Default.StopEstimationDMul,
                StopEstimationDDiv = Settings.Default.StopEstimationDDiv,
                CollectBasicIncome = new SubEpochEventHandler()
                {
                    Handler = settlementProcessor.HandleIncomeCollectionEvent,
                    DurationMul = Settings.Default.CollectBasicIncomeMul,
                    DurationDiv = Settings.Default.CollectBasicIncomeDiv,
                },
                DistributeBasicIncome = new SubEpochEventHandler()
                {
                    Handler = settlementProcessor.HandleIncomeDistributionEvent,
                    DurationMul = Settings.Default.DistributeBasicIncomeMul,
                    DurationDiv = Settings.Default.DistributeBasicIncomeDiv,
                }
            });
            blockTimers.Add(epochTimer);
            var emissionTimer = NewEmissionTimer(new EmitTimerArgs()
            {
                Processor = alphabetContractProcessor,
                EpochDuration = Settings.Default.AlphabetDuration
            });
            blockTimers.Add(emissionTimer);
        }

        public void InitConfigFromBlockchain()
        {
            ulong epoch;
            try
            {
                epoch = morphInvoker.Epoch();
            }
            catch (Exception e)
            {
                Utility.Log(nameof(InnerRingService), LogLevel.Debug, "can't read epoch" + e.Message);
                throw;
            }
            uint BalanceDecimals;
            try
            {
                BalanceDecimals = morphInvoker.BalanceDecimals();
            }
            catch
            {
                Utility.Log(nameof(InnerRingService), LogLevel.Debug, "can't read balance contract precision");
                throw;
            }
            SetEpochCounter((ulong)epoch);
            precision.SetBalanceDecimals(BalanceDecimals);
            Utility.Log(nameof(InnerRingService), LogLevel.Info, $"read config from blockchain, active={IsActive()}, epoch={epoch}, precision={BalanceDecimals}");
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
                    OnBlockEvent(blockEvent.Block, blockEvent.Flag);
                    break;
                case ContractEvent contractEvent:
                    OnContractEvent(contractEvent.Notify, contractEvent.Flag);
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
                morphEventListener.Tell(new Listener.BindBlockHandlerEvent()
                {
                    Handler = (Block b) =>
                    {
                        Utility.Log("MorphEventListener", LogLevel.Debug, $"new block, index={b.Index}");
                        TickTimers();
                    }
                });
                morphEventListener.Tell(new Listener.Start());
                mainEventListener.Tell(new Listener.Start());
                StartBlockTimers();
            }
            catch (Exception e)
            {
                Utility.Log(nameof(InnerRingService), LogLevel.Error, e.Message);
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
            if (flag) mainEventListener.Tell(new Listener.NewContractEvent() { Notify = notify });
            else morphEventListener.Tell(new Listener.NewContractEvent() { Notify = notify });
        }

        private void OnBlockEvent(Block block, bool flag)
        {
            if (flag) mainEventListener.Tell(new Listener.NewBlockEvent() { Block = block });
            else morphEventListener.Tell(new Listener.NewBlockEvent() { Block = block });
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
                Utility.Log(nameof(InnerRingService), LogLevel.Info, "ignore validator vote: node not in alphabet range");
                return;
            }
            if (!validators.Any())
            {
                Utility.Log(nameof(InnerRingService), LogLevel.Info, "ignore validator vote: empty validators list");
                return;
            }
            var epoch = EpochCounter();
            for (int i = 0; i < Settings.Default.AlphabetContractHash.Length; i++)
            {
                try
                {
                    morphInvoker.AlphabetVote(i, epoch, validators);
                }
                catch
                {
                    Utility.Log(nameof(InnerRingService), LogLevel.Info, $"can't invoke vote method in alphabet contract, alphabet_index={i}, epoch={epoch}");
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
            morphInvoker.PutAuditResult(res.ToByteArray());
        }

        public void ResetEpochTimer()
        {
            epochTimer.Reset();
        }

        public Action<Listen.Event.ContractEvent> OnlyActiveEventHandler(Action<Listen.Event.ContractEvent> f)
        {
            return (Listen.Event.ContractEvent morphEvent) => { if (IsActive()) f(morphEvent); };
        }

        public Action<Listen.Event.ContractEvent> OnlyAlphabetEventHandler(Action<Listen.Event.ContractEvent> f)
        {
            return (Listen.Event.ContractEvent morphEvent) => { if (IsAlphabet()) f(morphEvent); };
        }

        public void Dispose()
        {
            clientCache?.Dispose();
            auditContractProcessor.Dispose();
        }

        public static Props Props(NeoSystem main, NeoSystem side, Wallet pMainWallet = null, Wallet pSideWallet = null, MainInvoker pMainNetClient = null, MorphInvoker pMorphInvokerent = null)
        {
            return Akka.Actor.Props.Create(() => new InnerRingService(main, side, pMainWallet, pSideWallet, pMainNetClient, pMorphInvokerent));
        }
    }
}
