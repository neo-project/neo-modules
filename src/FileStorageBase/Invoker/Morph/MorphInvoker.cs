using Neo.FileStorage.Reputation;

namespace Neo.FileStorage.Invoker.Morph
{
    /// <summary>
    /// MorphClient is an implementation of the IClient interface.
    /// It is used to pre-execute invoking script and send script to the morph chain.
    /// </summary>
    public partial class MorphInvoker : ContractInvoker, INetmapSource
    {
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
