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

        private const string AlphabetContractNamePrefix = "alphabet";
        public const string AuditContractName = "audit";
        public const string BalanceContractName = "balance";
        public const string ContainerContractName = "container";
        public const string NeoFSIDContractName = "neofsid";
        public const string NetmapContractName = "netmap";
        public const string ProxyContractName = "proxy";
        public const string ReputationContractName = "reputation";

        public UInt160[] AlphabetContractHash;
        public UInt160 AuditContractHash;
        public UInt160 BalanceContractHash;
        public UInt160 ContainerContractHash;
        public UInt160 FsIdContractHash;
        public UInt160 NetMapContractHash;
        public UInt160 ReputationContractHash;

        public static string AlphabetContractName(int index)
        {
            return $"{AlphabetContractNamePrefix}{index}";
        }
    }
}
