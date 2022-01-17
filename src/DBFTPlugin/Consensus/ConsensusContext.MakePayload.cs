using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using static Neo.Consensus.RecoveryMessage;

namespace Neo.Consensus
{
    partial class ConsensusContext
    {
        public ExtensiblePayload MakeChangeView(ChangeViewReason reason)
        {
            return ChangeViewPayloads[MyIndex] = MakeSignedPayload(new ChangeView
            {
                Reason = reason,
                Timestamp = TimeProvider.Current.UtcNow.ToTimestampMS()
            });
        }

        public ExtensiblePayload MakeCommit(uint i)
        {
            return CommitPayloads[i][MyIndex] ?? (CommitPayloads[i][MyIndex] = MakeSignedPayload(new Commit
            {
                Signature = EnsureHeader(i).Sign(keyPair, neoSystem.Settings.Network),
                Id = i
            }));
        }

        private ExtensiblePayload MakeSignedPayload(ConsensusMessage message)
        {
            message.BlockIndex = Block[0].Index;
            message.ValidatorIndex = (byte)MyIndex;
            message.ViewNumber = ViewNumber;
            ExtensiblePayload payload = CreatePayload(message, null);
            SignPayload(payload);
            return payload;
        }

        private void SignPayload(ExtensiblePayload payload)
        {
            ContractParametersContext sc;
            try
            {
                sc = new ContractParametersContext(neoSystem.StoreView, payload, dbftSettings.Network);
                wallet.Sign(sc);
            }
            catch (InvalidOperationException)
            {
                return;
            }
            payload.Witness = sc.GetWitnesses()[0];
        }

        /// <summary>
        /// Prevent that block exceed the max size
        /// </summary>
        /// <param name="txs">Ordered transactions</param>
        internal void EnsureMaxBlockLimitation(IEnumerable<Transaction> txs, uint pID)
        {
            uint maxTransactionsPerBlock = neoSystem.Settings.MaxTransactionsPerBlock;

            // Limit Speaker proposal to the limit `MaxTransactionsPerBlock` or all available transactions of the mempool
            txs = txs.Take((int)maxTransactionsPerBlock);

            List<UInt256> hashes = new List<UInt256>();
            Transactions[pID] = new Dictionary<UInt256, Transaction>();
            VerificationContext[pID] = new TransactionVerificationContext();

            // Expected block size
            var blockSize = GetExpectedBlockSizeWithoutTransactions(txs.Count());
            var blockSystemFee = 0L;

            // Iterate transaction until reach the size or maximum system fee
            foreach (Transaction tx in txs)
            {
                // Check if maximum block size has been already exceeded with the current selected set
                blockSize += tx.Size;
                if (blockSize > dbftSettings.MaxBlockSize) break;

                // Check if maximum block system fee has been already exceeded with the current selected set
                blockSystemFee += tx.SystemFee;
                if (blockSystemFee > dbftSettings.MaxBlockSystemFee) break;

                hashes.Add(tx.Hash);
                Transactions[pID].Add(tx.Hash, tx);
                VerificationContext[pID].AddTransaction(tx);
            }

            TransactionHashes[pID] = hashes.ToArray();
        }

        public ExtensiblePayload MakePrepareRequest(uint pID)
        {
            Log($"MakePrepareRequest I", LogLevel.Debug);
            EnsureMaxBlockLimitation(neoSystem.MemPool.GetSortedVerifiedTransactions(), pID);
            Block[pID].Header.Timestamp = Math.Max(TimeProvider.Current.UtcNow.ToTimestampMS(), PrevHeader.Timestamp + 1);
            Block[pID].Header.Nonce = GetNonce();

            return PreparationPayloads[pID][MyIndex] = MakeSignedPayload(new PrepareRequest
            {
                Version = Block[pID].Version,
                PrevHash = Block[pID].PrevHash,
                Timestamp = Block[pID].Timestamp,
                Nonce = Block[pID].Nonce,
                TransactionHashes = TransactionHashes[pID]
            });
        }

        public ExtensiblePayload MakeRecoveryRequest()
        {
            return MakeSignedPayload(new RecoveryRequest
            {
                Timestamp = TimeProvider.Current.UtcNow.ToTimestampMS()
            });
        }

        public ExtensiblePayload MakeRecoveryMessage()
        {
            PrepareRequest prepareRequestMessage = null;
            if (TransactionHashes != null)
            {
                prepareRequestMessage = new PrepareRequest
                {
                    Version = Block[0].Version,
                    PrevHash = Block[0].PrevHash,
                    ViewNumber = ViewNumber,
                    Timestamp = Block[0].Timestamp,
                    Nonce = Block[0].Nonce,
                    BlockIndex = Block[0].Index,
                    TransactionHashes = TransactionHashes[0]
                };
            }
            return MakeSignedPayload(new RecoveryMessage
            {
                ChangeViewMessages = LastChangeViewPayloads.Where(p => p != null).Select(p => GetChangeViewPayloadCompact(p)).Take(M).ToDictionary(p => p.ValidatorIndex),
                PrepareRequestMessage = prepareRequestMessage,
                // We only need a PreparationHash set if we don't have the PrepareRequest information.
                PreparationHash = TransactionHashes[0] == null ? PreparationPayloads[0].Where(p => p != null).GroupBy(p => GetMessage<PrepareResponse>(p).PreparationHash, (k, g) => new { Hash = k, Count = g.Count() }).OrderByDescending(p => p.Count).Select(p => p.Hash).FirstOrDefault() : null,
                PreparationMessages = PreparationPayloads[0].Where(p => p != null).Select(p => GetPreparationPayloadCompact(p)).ToDictionary(p => p.ValidatorIndex),
                CommitMessages = CommitSent
                    ? CommitPayloads[0].Where(p => p != null).Select(p => GetCommitPayloadCompact(p)).ToDictionary(p => p.ValidatorIndex)
                    : new Dictionary<byte, CommitPayloadCompact>()
            });
        }

        public ExtensiblePayload MakePrepareResponse(uint i)
        {
            return PreparationPayloads[i][MyIndex] = MakeSignedPayload(new PrepareResponse
            {
                PreparationHash = PreparationPayloads[i][Block[i].PrimaryIndex].Hash,
                Id = i
            });
        }

        public ExtensiblePayload MakePreCommit(uint i)
        {
            return PreCommitPayloads[i][MyIndex] = MakeSignedPayload(new PreCommit
            {
                PreparationHash = PreCommitPayloads[i][Block[i].PrimaryIndex].Hash,
                Id = i
            });
        }

        private static ulong GetNonce()
        {
            Random _random = new();
            Span<byte> buffer = stackalloc byte[8];
            _random.NextBytes(buffer);
            return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }
    }
}
