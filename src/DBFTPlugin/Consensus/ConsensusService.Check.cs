using Akka.Actor;
using Neo.IO;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Consensus
{
    partial class ConsensusService
    {
        /// <summary>
        /// Need over 2f transaction lists
        /// valid transaction should exists in over 2f lists
        /// order are based on the time
        /// </summary>
        private void CheckTxLists()
        {
            // check for the primary
            if (context.TxListRequestSent && context.IsPrimary)
            {
                /// TODO: check the transactions
                if (context.TxlistsPayloads.Count(p => p != null) >= context.M && context.TransactionHashes.All(p => context.Transactions.ContainsKey(p)))
                {
                    tempTXs = new();
                    candidateTXs = new();
                    candidateTXHashs = new();
                    // 1. Get transaction that exists in more than 2f lists
                    foreach (var payload in context.TxlistsPayloads)
                    {
                        var list = context.GetMessage<TxListMessage>(payload);
                        for (int i = 0; i < list.Size; i++)
                        {
                            var hash = list.TransactionHashes[i];
                            if (!tempTXs.ContainsKey(hash))
                                tempTXs.Add(hash, new Tuple<int, int[]>(0, new int[] { i }));
                            else
                            {
                                var tuple = tempTXs[hash];
                                List<int> index = new(tuple.Item2) { 0 };
                                tempTXs[hash] = new Tuple<int, int[]>(tuple.Item1 + 1, index.ToArray());
                                // this is a valid transaction now
                                if (tuple.Item1 + 1 >= context.M)
                                {
                                    // Here, what if the primary does not have this transaction?
                                    // TODO:
                                    candidateTXHashs.Add(hash, index.ToArray());
                                    candidateTXs.Add(context.Transactions[hash]);
                                }
                            }
                        }
                    }

                    // 2. Order the transactions according to the transaction fee
                    var feeOrderedTXs = candidateTXs.OrderBy(p => p.NetworkFee + p.SystemFee).ToArray();

                    // 3. Only keep the max n transactions
                    context.EnsureMaxBlockLimitation(candidateTXs);

                    // 4. Reorder these transactions according to the index
                    var indexOrderedTxs = feeOrderedTXs.OrderBy(p =>
                    {
                        var index = candidateTXHashs[p.Hash];
                        var len = index.Length;
                        int[,] densities = new int[len, len];
                        Dictionary<int, int> outliers = new();

                        // Find f outlier values
                        for (int i = 0; i < len; i++)
                        {
                            for (int j = len - 1; j < len; j++)
                            {
                                outliers[i] += densities[i, j];
                            }
                            for (int k = i + 1; k < len; k++)
                            {
                                var outlier = Math.Abs(index[i] - index[k]);
                                densities[i, k] = outlier;
                                outliers[i] += outlier;
                            }
                        }

                        return outliers.ToList()
                            .OrderByDescending(p => p.Value)
                            .Take(len - context.F)
                            .ToList()
                            .Average(pair => index[pair.Key]);
                    }).ToArray();
                    // 5. Randomize those with same transaction fee and index
                    // TODO: leave it for future work

                    Dictionary<UInt256, Transaction> txs = new();
                    context.Transactions.Clear();
                    // 6. Pack those transactions in a new transaction list
                    foreach (var v in indexOrderedTxs)
                    {
                        context.Transactions .Add(v.Hash, v);
                    }
                    context.TransactionHashes = context.Transactions.Select(p => p.Key).ToArray();
                    // 7. broadcast the new transaction list along with lists from other CNs

                    // Update the hashes
                    SendPrepareRequest(context.TransactionHashes);
                }
            }
            if (!context.TxListRequestSent && !context.IsPrimary)
            {

            }


        }

        private bool CheckPrepareResponse()
        {
            if (context.TransactionHashes.Length == context.Transactions.Count)
            {
                // if we are the primary for this view, but acting as a backup because we recovered our own
                // previously sent prepare request, then we don't want to send a prepare response.
                if (context.IsPrimary || context.WatchOnly) return true;

                // Check maximum block size via Native Contract policy
                if (context.GetExpectedBlockSize() > dbftSettings.MaxBlockSize)
                {
                    Log($"Rejected block: {context.Block.Index} The size exceed the policy", LogLevel.Warning);
                    RequestChangeView(ChangeViewReason.BlockRejectedByPolicy);
                    return false;
                }
                // Check maximum block system fee via Native Contract policy
                if (context.GetExpectedBlockSystemFee() > dbftSettings.MaxBlockSystemFee)
                {
                    Log($"Rejected block: {context.Block.Index} The system fee exceed the policy", LogLevel.Warning);
                    RequestChangeView(ChangeViewReason.BlockRejectedByPolicy);
                    return false;
                }

                // Timeout extension due to prepare response sent
                // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
                ExtendTimerByFactor(2);

                Log($"Sending {nameof(PrepareResponse)}");
                localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakePrepareResponse() });
                CheckPreparations();
            }
            return true;
        }

        private void CheckCommits()
        {
            if (context.CommitPayloads.Count(p => context.GetMessage(p)?.ViewNumber == context.ViewNumber) >= context.M && context.TransactionHashes.All(p => context.Transactions.ContainsKey(p)))
            {
                block_received_index = context.Block.Index;
                block_received_time = TimeProvider.Current.UtcNow;
                Block block = context.CreateBlock();
                Log($"Sending {nameof(Block)}: height={block.Index} hash={block.Hash} tx={block.Transactions.Length}");
                blockchain.Tell(block);
            }
        }

        private void CheckExpectedView(byte viewNumber)
        {
            if (context.ViewNumber >= viewNumber) return;
            var messages = context.ChangeViewPayloads.Select(p => context.GetMessage<ChangeView>(p)).ToArray();
            // if there are `M` change view payloads with NewViewNumber greater than viewNumber, then, it is safe to move
            if (messages.Count(p => p != null && p.NewViewNumber >= viewNumber) >= context.M)
            {
                if (!context.WatchOnly)
                {
                    ChangeView message = messages[context.MyIndex];
                    // Communicate the network about my agreement to move to `viewNumber`
                    // if my last change view payload, `message`, has NewViewNumber lower than current view to change
                    if (message is null || message.NewViewNumber < viewNumber)
                        localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakeChangeView(ChangeViewReason.ChangeAgreement) });
                }
                InitializeConsensus(viewNumber);
            }
        }

        private void CheckPreparations()
        {
            if (context.PreparationPayloads.Count(p => p != null) >= context.M && context.TransactionHashes.All(p => context.Transactions.ContainsKey(p)))
            {
                ExtensiblePayload payload = context.MakeCommit();
                Log($"Sending {nameof(Commit)}");
                context.Save();
                localNode.Tell(new LocalNode.SendDirectly { Inventory = payload });
                // Set timer, so we will resend the commit in case of a networking issue
                ChangeTimer(TimeSpan.FromMilliseconds(neoSystem.Settings.MillisecondsPerBlock));
                CheckCommits();
            }
        }
    }
}
