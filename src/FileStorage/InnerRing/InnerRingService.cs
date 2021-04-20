using Akka.Actor;
using Neo.SmartContract;
using System;
using Neo.Plugins.util;
using Neo.Wallets.NEP6;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Neo.Cryptography.ECC;
using Neo.IO;
using Google.Protobuf;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Services.Audit;
using Neo.FileStorage.Morph.Event;
using static Neo.FileStorage.Morph.Event.Listener;
using Neo.FileStorage.InnerRing.Timer;
using Neo.FileStorage.API.Audit;
using static Neo.FileStorage.InnerRing.Timer.EpochTickEvent;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.Utils;
using static Neo.FileStorage.InnerRing.Timer.BlockTimer;
using static Neo.FileStorage.InnerRing.Timer.Helper;

namespace Neo.FileStorage.InnerRing
{
    /// <summary>
    /// InneringService is the entry for processing all events related to the inner ring node.
    /// All events will be distributed according to type.(2 event types:MainContractEvent and MorphContractEvent)
    /// Life process:Start--->Assignment event--->Stop
    /// </summary>
    public class InnerRingService : UntypedActor, IState
    {
        public class MainContractEvent { public NotifyEventArgs notify; };
        public class MorphContractEvent { public NotifyEventArgs notify; };
        public class Start { };
        public class Stop { };
        // event producers
        private IActorRef morphEventListener;
        private IActorRef mainEventListener;
        private List<IActorRef> blockTimers;
        private IActorRef epochTimer;

        // global state
        private Client mainNetClient;
        private Client morphClient;
        private long epochCounter;
        private InnerRingIndexer statusIndex;
        private Fixed8ConverterUtil precision;
        private IActorRef timer;
        private IActorRef auditTaskManager;
        // internal variables
        private readonly NEP6Wallet wallet;
        private List<IActorRef> workers;

        private BalanceContractProcessor balanceContractProcessor;
        private ContainerContractProcessor containerContractProcessor;
        private FsContractProcessor fsContractProcessor;
        private NetMapContractProcessor netMapContractProcessor;
        private GovernanceProcessor governanceProcessor;
        private SettlementProcessor settlementProcessor;

        private AlphabetContractProcessor alphabetContractProcessor;
        private AuditContractProcessor auditContractProcessor;

        private RpcClientCache clientCache;

        private List<Action> starters;
        private List<Action> closers;

        /// <summary>
        /// Constructor.
        /// 4 Tasks:
        /// 1)Build mainnet and morph clients
        /// 2)Build mainnet and morph contract event handlers
        /// 3)Build mainnet and morph event listeners
        /// 4)Initialization
        /// </summary>
        /// <param name="system">NeoSystem</param>
        public InnerRingService(NeoSystem system, NEP6Wallet pwallet = null, Client pMainNetClient = null, Client pMorphClient = null)
        {
            precision = new Fixed8ConverterUtil();
            //Create wallet
            if (pwallet is null)
            {
                wallet = new NEP6Wallet(Settings.Default.WalletPath, system.Settings);
                wallet.Unlock(Settings.Default.Password);
            }
            else
            {
                wallet = pwallet;
            }
            //Build 2 clients(MainNetClient&MorphClient).
            if (pMainNetClient is null)
            {
                mainNetClient = new Client() { client = new MainClient(Settings.Default.Urls, wallet) };
            }
            else
            {
                mainNetClient = pMainNetClient;
            }
            if (pMorphClient is null)
            {
                morphClient = new Client()
                {
                    client = new MorphClient()
                    {
                        wallet = wallet,
                        system = system,
                    }
                };
            }
            else
            {
                morphClient = pMorphClient;
            }

            //Build 2 listeners(MorphEventListener&MainEventListener).
            morphEventListener = system.ActorSystem.ActorOf(Listener.Props("MorphEventListener"));
            mainEventListener = system.ActorSystem.ActorOf(Listener.Props("MainEventListener"));
            statusIndex = new InnerRingIndexer(morphClient, Settings.Default.IndexerTimeout);

            //todo

            // create audit processor
            clientCache = new RpcClientCache() { wallet = wallet };
            auditTaskManager = system.ActorSystem.ActorOf(Manager.Props(Settings.Default.QueueCapacity, clientCache, Settings.Default.MaxPDPSleepInterval));
            auditContractProcessor = new AuditContractProcessor()
            {
                MorphCli = morphClient,
                ClientCache = clientCache,
                TaskManager = auditTaskManager,
                reporter = this,
            };
            // create settlement processor dependencies
            //todo

            // create governance processor
            governanceProcessor = new GovernanceProcessor()
            {
                MainCli = mainNetClient,
                MorphCli = morphClient,
            };
            // create netmap processor
            //todo
            netMapContractProcessor = new NetMapContractProcessor()
            {
                MorphCli = morphClient,
                State = this,
                NetmapSnapshot = new NetMapContractProcessor.CleanupTable(Settings.Default.CleanupEnabled, Settings.Default.CleanupThreshold),
                WorkPool = system.ActorSystem.ActorOf(WorkerPool.Props("NetMapContract Processor", Settings.Default.NetmapContractWorkersSize))
            };
            morphEventListener.Tell(new BindProcessorEvent() { processor = netMapContractProcessor });
            // create container processor
            containerContractProcessor = new ContainerContractProcessor()
            {
                MorphCli = morphClient,
                State = this,
                WorkPool = system.ActorSystem.ActorOf(WorkerPool.Props("ContainerContract Processor", Settings.Default.ContainerContractWorkersSize))
            };
            morphEventListener.Tell(new BindProcessorEvent() { processor = containerContractProcessor });
            // create balance processor
            balanceContractProcessor = new BalanceContractProcessor()
            {
                MainCli = mainNetClient,
                Convert = precision,
                State = this,
                WorkPool = system.ActorSystem.ActorOf(WorkerPool.Props("BalanceContract Processor", Settings.Default.BalanceContractWorkersSize))
            };
            morphEventListener.Tell(new BindProcessorEvent() { processor = balanceContractProcessor });
            // todo: create reputation processor
            // create  neofs processor
            fsContractProcessor = new FsContractProcessor()
            {
                MorphCli = morphClient,
                Convert = precision,
                State = this,
                WorkPool = system.ActorSystem.ActorOf(WorkerPool.Props("FsContract Processor", Settings.Default.FsContractWorkersSize))
            };
            mainEventListener.Tell(new BindProcessorEvent() { processor = fsContractProcessor });
            // create alphabet processor
            alphabetContractProcessor = new AlphabetContractProcessor()
            {
                MorphCli = morphClient,
                StorageEmission = Settings.Default.StorageEmission,
                WorkPool = system.ActorSystem.ActorOf(WorkerPool.Props("AlphabetContract Processor", Settings.Default.AlphabetContractWorkersSize))
            };
            // todo: create vivid id component
            // initialize epoch timers
            epochTimer = NewEpochTimer(new EpochTimerArgs()
            {
                context = system,
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
            var emissionTimer = Timer.Helper.NewEmissionTimer(new EmitTimerArgs()
            {
                context = system,
                processor = alphabetContractProcessor,
                epochDuration = Settings.Default.AlphabetDuration
            });
            blockTimers.Add(emissionTimer);
            //todo
            //notary
        }

        public void InitConfigFromBlockchain()
        {
            long epoch;
            try
            {
                epoch = morphClient.GetEpoch();
            }
            catch
            {
                throw new Exception("can't read epoch");
            }
            uint balancePrecision;
            try
            {
                balancePrecision = morphClient.BalancePrecision();
            }
            catch
            {
                throw new Exception("can't read balance contract precision");
            }
            SetEpochCounter((ulong)epoch);
            precision.SetBalancePrecision(balancePrecision);
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("read config from blockchain", ":");
            pairs.Add("active", IsActive().ToString());
            pairs.Add("epoch", epoch.ToString());
            pairs.Add("precision", balancePrecision.ToString());
            Utility.Log("InnerRingService", LogLevel.Info, pairs.ParseToString());
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
                case MainContractEvent mainEvent:
                    OnMainContractEvent(mainEvent.notify);
                    break;
                case MorphContractEvent morphEvent:
                    OnMorphContractEvent(morphEvent.notify);
                    break;
                default:
                    break;
            }
        }

        private void OnStart()
        {
            try
            {
                foreach (var starter in starters)
                    starter();
                InitConfigFromBlockchain();
                VoteForSidechainValidator(Settings.Default.validators);
                morphEventListener.Tell(new Listener.Start());
                mainEventListener.Tell(new Listener.Start());
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
                blockTimer.Tell(new ResetEvent());
            }
        }

        private void StartWorkers()
        {
            foreach (var blockTimer in blockTimers)
            {
                blockTimer.Tell(new ResetEvent());
            }
        }

        private void OnStop()
        {
            morphEventListener.Tell(new Listener.Stop());
            mainEventListener.Tell(new Listener.Stop());
            foreach (var closer in closers) closer();
        }





        private void OnMainContractEvent(NotifyEventArgs notify)
        {
            Console.WriteLine("接收到主网事件：" + notify.ScriptHash.ToString() + ";" + notify.EventName);
            mainEventListener.Tell(new NewContractEvent() { notify = notify });
        }

        private void OnMorphContractEvent(NotifyEventArgs notify)
        {
            morphEventListener.Tell(new NewContractEvent() { notify = notify });
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
            if (InnerRingIndex() < 0 || InnerRingIndex() >= Settings.Default.AlphabetContractHash.Length)
            {
                Utility.Log("", LogLevel.Info, "ignore validator vote: node not in alphabet range");
                return;
            }
            if (validators.Length == 0)
            {
                Utility.Log("", LogLevel.Info, "ignore validator vote: empty validators list");
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
                    Utility.Log("", LogLevel.Info, string.Format("can't invoke vote method in alphabet contract,alphabet_index:{0},epoch:{1}}", i, epoch));
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
            IEnumerable<Wallets.WalletAccount> accounts = wallet.GetAccounts();
            DataAuditResult res = r.Result();
            res.PublicKey = ByteString.CopyFrom(accounts.ToArray()[0].GetKey().PublicKey.ToArray());
            morphClient.InvokePutAuditResult(res.ToByteArray());
        }

        public void ResetEpochTimer()
        {
            epochTimer.Tell(new ResetEvent());
        }

        public static Props Props(NeoSystem system, NEP6Wallet pwallet = null, Client pMainNetClient = null, Client pMorphClient = null)
        {
            return Akka.Actor.Props.Create(() => new InnerRingService(system, pwallet, pMainNetClient, pMorphClient));
        }
    }
}
