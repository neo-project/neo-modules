using System.Numerics;
using Akka.Actor;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Listen;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class BaseProcessor : IProcessor
    {
        public static readonly BigInteger BigGB = new(1 << 30);
        public static readonly BigInteger BigZero = new(0);
        public static readonly BigInteger BigOne = new(1);

        public virtual string Name => "BaseProcessor";
        public static UInt160 ContainerContractHash => Settings.Default.ContainerContractHash;
        public static UInt160 FsContractHash => Settings.Default.FsContractHash;
        public static UInt160 BalanceContractHash => Settings.Default.BalanceContractHash;
        public static UInt160 NetmapContractHash => Settings.Default.NetmapContractHash;
        public static UInt160 FsIdContractHash => Settings.Default.FsIdContractHash;
        public static UInt160 AuditContractHash => Settings.Default.AuditContractHash;
        public static UInt160 ReputationContractHash => Settings.Default.AuditContractHash;

        public MainInvoker MainInvoker;
        public MorphInvoker MorphInvoker;
        public IState State;
        public IActorRef WorkPool;
        public ProtocolSettings ProtocolSettings;

        public virtual HandlerInfo[] ListenerHandlers()
        {
            return System.Array.Empty<HandlerInfo>();
        }

        public virtual ParserInfo[] ListenerParsers()
        {
            return System.Array.Empty<ParserInfo>();
        }

        public virtual HandlerInfo[] TimersHandlers()
        {
            return System.Array.Empty<HandlerInfo>();
        }
    }
}
