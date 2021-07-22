using System.Numerics;
using Akka.Actor;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Morph.Listen;
using Neo.VM.Types;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class BaseProcessor : IProcessor
    {
        public static readonly BigInteger bigGB = new(1 << 30);
        public static readonly BigInteger bigZero = new(0);
        public static readonly BigInteger bigOne = new(1);

        public virtual string Name => "BaseProcessor";
        public UInt160 ContainerContractHash => Settings.Default.ContainerContractHash;
        public UInt160 FsContractHash => Settings.Default.FsContractHash;
        public UInt160 BalanceContractHash => Settings.Default.BalanceContractHash;
        public UInt160 NetmapContractHash => Settings.Default.NetmapContractHash;
        public UInt160 FsIdContractHash => Settings.Default.FsIdContractHash;
        public UInt160 AuditContractHash => Settings.Default.AuditContractHash;
        public UInt160 ReputationContractHash => Settings.Default.AuditContractHash;

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
