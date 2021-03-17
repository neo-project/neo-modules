using Akka.Actor;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.InnerRing.Timer;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Invoker;
using Neo.SmartContract;
using System;
using static Neo.FileStorage.InnerRing.Timer.Timers;
using static Neo.FileStorage.Morph.Event.Listener;
using Neo.Plugins.util;
using Neo.Wallets.NEP6;
using System.Collections.Generic;
using System.Linq;
using static Neo.FileStorage.InnerRing.Timer.EpochTickEvent;
using System.Threading;
using Neo.Cryptography.ECC;
using Neo.Plugins.Innerring.Processors;
using Neo.FileStorage.Services.Audit;
using Neo.FileStorage.API.Audit;
using Neo.IO;
using Google.Protobuf;

namespace Neo.FileStorage.InnerRing
{
    /// <summary>
    /// InneringService is the entry for processing all events related to the inner ring node.
    /// All events will be distributed according to type.(2 event types:MainContractEvent and MorphContractEvent)
    /// Life process:Start--->Assignment event--->Stop
    /// </summary>
    public class InnerRingService : UntypedActor, IActiveState, IEpochState, IEpochTimerReseter, IIndexer, IReporter
    {
        public class MainContractEvent { public NotifyEventArgs notify; };
        public class MorphContractEvent { public NotifyEventArgs notify; };
        public class Start { };
        public class Stop { };

        private IActorRef morphEventListener;
        private IActorRef mainEventListener;
        private IActorRef timer;
        private IActorRef auditTaskManager;

        private BalanceContractProcessor balanceContractProcessor;
        private ContainerContractProcessor containerContractProcessor;
        private FsContractProcessor fsContractProcessor;
        private NetMapContractProcessor netMapContractProcessor;
        private AlphabetContractProcessor alphabetContractProcessor;
        private AuditContractProcessor auditContractProcessor;

        private IClient mainNetClient;
        private IClient morphClient;
        private RpcClientCache clientCache;
        private readonly NEP6Wallet wallet;
        private long epochCounter;
        private int indexer = -1;
        private int indexerSize = 0;

        private Fixed8ConverterUtil convert;

        /// <summary>
        /// Constructor.
        /// 4 Tasks:
        /// 1)Build mainnet and morph clients
        /// 2)Build mainnet and morph contract event handlers
        /// 3)Build mainnet and morph event listeners
        /// 4)Initialization
        /// </summary>
        /// <param name="system">NeoSystem</param>
        public InnerRingService(NeoSystem system, NEP6Wallet pwallet = null, IClient pMainNetClient = null, IClient pMorphClient = null)
        {
            convert = new Fixed8ConverterUtil();
            //Create wallet
            if (pwallet is null)
            {
                wallet = new NEP6Wallet(Settings.Default.WalletPath);
                wallet.Unlock(Settings.Default.Password);
            }
            else
            {
                wallet = pwallet;
            }
            //Build 2 clients(MainNetClient&MorphClient).
            if (pMainNetClient is null)
            {
                mainNetClient = new MainClient(Settings.Default.Urls, wallet);
            }
            else
            {
                mainNetClient = pMainNetClient;
            }
            if (pMorphClient is null)
            {
                morphClient = new MorphClient()
                {
                    Wallet = wallet,
                    Blockchain = system.Blockchain,
                };
            }
            else
            {
                morphClient = pMorphClient;
            }
            //Build processor of contract.
            balanceContractProcessor = new BalanceContractProcessor()
            {
                Client = mainNetClient,
                Convert = convert,
                ActiveState = this,
                WorkPool = system.ActorSystem.ActorOf(WorkerPool.Props("BalanceContract Processor", Settings.Default.BalanceContractWorkersSize))
            };
            containerContractProcessor = new ContainerContractProcessor()
            {
                Client = morphClient,
                ActiveState = this,
                WorkPool = system.ActorSystem.ActorOf(WorkerPool.Props("ContainerContract Processor", Settings.Default.ContainerContractWorkersSize))
            };
            fsContractProcessor = new FsContractProcessor()
            {
                Client = morphClient,
                Convert = convert,
                ActiveState = this,
                EpochState = this,
                WorkPool = system.ActorSystem.ActorOf(WorkerPool.Props("FsContract Processor", Settings.Default.FsContractWorkersSize))
            };
            netMapContractProcessor = new NetMapContractProcessor()
            {
                Client = morphClient,
                ActiveState = this,
                EpochState = this,
                EpochTimerReseter = this,
                NetmapSnapshot = new NetMapContractProcessor.CleanupTable(Settings.Default.CleanupEnabled, Settings.Default.CleanupThreshold),
                WorkPool = system.ActorSystem.ActorOf(WorkerPool.Props("NetMapContract Processor", Settings.Default.NetmapContractWorkersSize))
            };
            alphabetContractProcessor = new AlphabetContractProcessor()
            {
                Client = morphClient,
                Indexer = this,
                StorageEmission = Settings.Default.StorageEmission,
                WorkPool = system.ActorSystem.ActorOf(WorkerPool.Props("AlphabetContract Processor", Settings.Default.AlphabetContractWorkersSize))
            };
            clientCache = new RpcClientCache() { wallet = wallet };
            auditTaskManager = system.ActorSystem.ActorOf(Manager.Props(Settings.Default.QueueCapacity, clientCache, Settings.Default.MaxPDPSleepInterval));

            auditContractProcessor = new AuditContractProcessor()
            {
                Client = morphClient,
                Indexer = this,
                ClientCache = clientCache,
                TaskManager = auditTaskManager,
                reporter = this,
            };
            //Build listener
            morphEventListener = system.ActorSystem.ActorOf(Listener.Props("MorphEventListener"));
            mainEventListener = system.ActorSystem.ActorOf(Listener.Props("MainEventListener"));
            morphEventListener.Tell(new BindProcessorEvent() { processor = netMapContractProcessor });
            morphEventListener.Tell(new BindProcessorEvent() { processor = containerContractProcessor });
            morphEventListener.Tell(new BindProcessorEvent() { processor = balanceContractProcessor });
            mainEventListener.Tell(new BindProcessorEvent() { processor = fsContractProcessor });
            //Build timer
            timer = system.ActorSystem.ActorOf(Timers.Props());
            timer.Tell(new BindTimersEvent() { processor = netMapContractProcessor });
            timer.Tell(new BindTimersEvent() { processor = containerContractProcessor });
            timer.Tell(new BindTimersEvent() { processor = balanceContractProcessor });
            timer.Tell(new BindTimersEvent() { processor = fsContractProcessor });
        }

        public void InitConfig()
        {
            long epoch;
            try
            {
                epoch = ContractInvoker.GetEpoch(morphClient);
            }
            catch
            {
                throw new Exception("can't read epoch");
            }
            var key = wallet.GetAccounts().ToArray()[0].GetKey().PublicKey;
            int index;
            int size;
            try
            {
                index = ContractInvoker.InnerRingIndex(mainNetClient, key, out size);
            }
            catch
            {
                throw new Exception("can't read inner ring list");
            }
            uint balancePrecision;
            try
            {
                balancePrecision = ContractInvoker.BalancePrecision(morphClient);
            }
            catch
            {
                throw new Exception("can't read balance contract precision");
            }

            SetEpochCounter((ulong)epoch);
            SetIndexer(index);
            SetInnerRingSize(size);
            convert.SetBalancePrecision(balancePrecision);
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("read config from blockchain", ":");
            pairs.Add("active", IsActive().ToString());
            pairs.Add("epoch", epoch.ToString());
            pairs.Add("precision", balancePrecision.ToString());
            Neo.Utility.Log("", LogLevel.Info, pairs.ParseToString());
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
                InitAndVoteForSidechainValidator(Settings.Default.validators);
                morphEventListener.Tell(new Listener.Start());
                mainEventListener.Tell(new Listener.Start());
                timer.Tell(new Timers.Start());
            }
            catch (Exception e)
            {
                Neo.Utility.Log("", LogLevel.Error, e.Message);
                return;
            }
        }

        private void OnStop()
        {
            timer.Tell(new Timers.Stop());
            morphEventListener.Tell(new Listener.Stop());
            mainEventListener.Tell(new Listener.Stop());
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

        public bool IsActive()
        {
            return Index() >= 0;
        }

        public void SetEpochCounter(ulong epoch)
        {
            long temp = Convert.ToInt64(epoch);
            Interlocked.Exchange(ref epochCounter, temp);
        }

        public int Index()
        {
            return indexer;
        }

        public void SetIndexer(int index)
        {
            Interlocked.Exchange(ref indexer, index);
        }

        public void SetInnerRingSize(int size)
        {
            Interlocked.Exchange(ref indexerSize, size);
        }

        public int InnerRingSize()
        {
            return indexerSize;
        }

        public ulong EpochCounter()
        {
            return Convert.ToUInt64(epochCounter);
        }

        public void ResetEpochTimer()
        {
            timer.Tell(new Timers.Timer() { contractEvent = new NewEpochTickEvent() { } });
        }

        public void VoteForSideChainValidator(ECPoint[] validators)
        {
            if (Index() < 0 || Index() >= Settings.Default.AlphabetContractHash.Length)
            {
                Neo.Utility.Log("", LogLevel.Info, "ignore validator vote: node not in alphabet range");
                return;
            }
            if (validators.Length == 0)
            {
                Neo.Utility.Log("", LogLevel.Info, "ignore validator vote: empty validators list");
                return;
            }
            var epoch = EpochCounter();
            for (int i = 0; i < Settings.Default.AlphabetContractHash.Length; i++)
            {
                try
                {
                    ContractInvoker.AlphabetVote(morphClient, i, epoch, validators);
                }
                catch
                {
                    Neo.Utility.Log("", LogLevel.Info, string.Format("can't invoke vote method in alphabet contract,alphabet_index:{0},epoch:{1}}", i, epoch));
                }
            }
        }

        public void InitAndVoteForSidechainValidator(ECPoint[] validators)
        {
            InitConfig();
            VoteForSideChainValidator(validators);
        }

        public void WriteReport(Report r)
        {
            IEnumerable<Wallets.WalletAccount> accounts = wallet.GetAccounts();
            DataAuditResult res = r.Result();
            res.PublicKey = ByteString.CopyFrom(accounts.ToArray()[0].GetKey().PublicKey.ToArray());
            MorphContractInvoker.InvokePutAuditResult(morphClient, res.ToByteArray());
        }

        public static Props Props(NeoSystem system, NEP6Wallet pwallet = null, IClient pMainNetClient = null, IClient pMorphClient = null)
        {
            return Akka.Actor.Props.Create(() => new InnerRingService(system, pwallet, pMainNetClient, pMorphClient));
        }
    }
}
