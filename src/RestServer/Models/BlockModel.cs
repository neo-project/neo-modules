namespace Neo.Plugins.RestServer.Models
{
    public class BlockModel
    {
        public ulong Timestamp { get; set; }
        public uint Version { get; set; }
        public byte PrimaryIndex { get; set; }
        public uint Index { get; set; }
        public ulong Nonce { get; set; }
        public int Size { get; set; }
        public UInt256 Hash { get; set; }
        public UInt256 MerkleRoot { get; set; }
        public UInt256 PrevHash { get; set; }
        public UInt160 NextConsensus { get; set; }
        public WitnessModel Witness { get; set; }
        public IEnumerable<BlockTransactionModel> Transactions { get; set; }
    }
}
