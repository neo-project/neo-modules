using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Native;

namespace Neo.Plugins
{
    static class Utilities
    {
        public static Block CreateDummyBlockWithTimestamp(DataCache snapshot, ProtocolSettings settings, ulong timestamp = 0)
        {
            UInt256 hash = NativeContract.Ledger.CurrentHash(snapshot);
            Block currentBlock = NativeContract.Ledger.GetBlock(snapshot, hash);
            return new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = hash,
                    MerkleRoot = new UInt256(),
                    Timestamp = timestamp == 0 ? currentBlock.Timestamp + settings.MillisecondsPerBlock : timestamp,
                    Index = currentBlock.Index + 1,
                    NextConsensus = currentBlock.NextConsensus,
                    Witness = new Witness
                    {
                        InvocationScript = Array.Empty<byte>(),
                        VerificationScript = Array.Empty<byte>()
                    },
                },
                Transactions = Array.Empty<Transaction>()
            };
        }
    }
}
