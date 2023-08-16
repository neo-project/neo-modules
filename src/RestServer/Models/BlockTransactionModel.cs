namespace Neo.Plugins.RestServer.Models
{
    public class BlockTransactionModel
    {
        public UInt256 Hash { get; set; }
        public UInt160 Sender { get; set; }

        public ReadOnlyMemory<byte> Script { get; set; }

        public long FeePerByte { get; set; }
        public long NetworkFee { get; set; }
        public long SystemFee { get; set; }
        public int Size { get; set; }

        public uint Nonce { get; set; }
        public byte Version { get; set; }
        public uint ValidUntilBlock { get; set; }

        public IEnumerable<WitnessModel> Witnesses { get; set; }
        public IEnumerable<SignerModel> Signers { get; set; }
        public IEnumerable<TransactionAttributeModel> Attributes { get; set; }
    }
}
