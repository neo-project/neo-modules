using Neo.Network.P2P.Payloads;
using System.Linq;

namespace Neo.Network.RPC.Tests
{
    internal static class TestUtils
    {
        public static Block GetBlock(int txCount)
        {
            return new Block
            {
                PrevHash = UInt256.Zero,
                MerkleRoot = UInt256.Zero,
                NextConsensus = UInt160.Zero,
                Witness = new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new byte[0]
                },
                ConsensusData = new ConsensusData(),
                Transactions = Enumerable.Range(0, txCount).Select(p => GetTransaction()).ToArray()
            };
        }

        public static Header GetHeader()
        {
            return GetBlock(0).Header;
        }

        public static Transaction GetTransaction()
        {
            return new Transaction
            {
                Script = new byte[1],
                Sender = UInt160.Zero,
                Attributes = new TransactionAttribute[0],
                Cosigners = new Cosigner[0],
                Witnesses = new Witness[]
                {
                    new Witness
                    {
                        InvocationScript = new byte[0],
                        VerificationScript = new byte[0]
                    }
                }
            };
        }
    }
}
