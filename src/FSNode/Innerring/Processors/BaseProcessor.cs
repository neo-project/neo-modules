using Akka.Actor;
using Neo.Plugins.FSStorage;
using Neo.Plugins.FSStorage.innerring.processors;
using Neo.Plugins.FSStorage.morph.invoke;
using Settings = Neo.Plugins.FSStorage.Settings;

namespace Neo.Plugins.Innerring.Processors
{
    public class BaseProcessor : IProcessor
    {
        public virtual string Name => "BaseProcessor";
        public UInt160 ContainerContractHash => Settings.Default.ContainerContractHash;
        public UInt160 FsContractHash => Settings.Default.FsContractHash;
        public UInt160 BalanceContractHash => Settings.Default.BalanceContractHash;
        public UInt160 NetmapContractHash => Settings.Default.NetmapContractHash;
        public UInt160 FsIdContractHash => Settings.Default.FsIdContractHash;
        public UInt160 AuditContractHash => Settings.Default.AuditContractHash;
        private Client morphCli;
        private Client mainCli;
        public IIndexer indexer;
        public IActiveState activeState;
        public IActorRef workPool;
        public IEpochState epochState;
        public IVoter voter;

        public Client MainCli { get => mainCli; set => mainCli = value; }
        public Client MorphCli { get => morphCli; set => morphCli = value; }
        public IIndexer Indexer { get => indexer; set => indexer = value; }
        public IActiveState ActiveState { get => activeState; set => activeState = value; }
        public IActorRef WorkPool { get => workPool; set => workPool = value; }
        public IEpochState EpochState { get => epochState; set => epochState = value; }
        public IVoter Voter { get => voter; set => voter = value; }


        public virtual bool IsActive()
        {
            return ActiveState.IsActive();
        }

        public virtual ulong EpochCounter()
        {
            return EpochState.EpochCounter();
        }

        public virtual void SetEpochCounter(ulong epoch)
        {
            EpochState.SetEpochCounter(epoch);
        }

        public virtual HandlerInfo[] ListenerHandlers()
        {
            return new HandlerInfo[] { };
        }

        public virtual ParserInfo[] ListenerParsers()
        {
            return new ParserInfo[] { };
        }

        public virtual HandlerInfo[] TimersHandlers()
        {
            return new HandlerInfo[] { };
        }
    }
}
