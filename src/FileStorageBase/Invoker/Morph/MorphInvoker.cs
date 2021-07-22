using Akka.Actor;
using Neo.FileStorage.Reputation;
using Neo.Wallets;

namespace Neo.FileStorage.Invoker.Morph
{
    /// <summary>
    /// MorphClient is an implementation of the IClient interface.
    /// It is used to pre-execute invoking script and send script to the morph chain.
    /// </summary>
    public partial class MorphInvoker : ContractInvoker, INetmapSource
    {
        public Wallet Wallet { get; init; }
        public NeoSystem NeoSystem { get; init; }
        public IActorRef Blockchain { get; init; }
        public long SideChainFee { get; init; }
        public UInt160[] AlphabetContractHash { get; init; }
        public UInt160 AuditContractHash { get; init; }
        public UInt160 BalanceContractHash { get; init; }
        public UInt160 ContainerContractHash { get; init; }
        public UInt160 FsIdContractHash { get; init; }
        public UInt160 NetMapContractHash { get; init; }
        public UInt160 ReputationContractHash { get; init; }
    }
}
