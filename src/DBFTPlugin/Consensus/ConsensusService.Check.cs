// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Consensus.DBFT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.IO;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using System;
using System.Linq;

namespace Neo.Consensus
{
    partial class ConsensusService
    {
        private bool CheckPrepareResponse()
        {
            if (_context.TransactionHashes.Length == _context.Transactions.Count)
            {
                // if we are the primary for this view, but acting as a backup because we recovered our own
                // previously sent prepare request, then we don't want to send a prepare response.
                if (_context.IsPrimary || _context.WatchOnly) return true;

                // Check maximum block size via Native Contract policy
                if (_context.GetExpectedBlockSize() > _dbftSettings.MaxBlockSize)
                {
                    Log($"Rejected block: {_context.Block.Index} The size exceed the policy", LogLevel.Warning);
                    RequestChangeView(ChangeViewReason.BlockRejectedByPolicy);
                    return false;
                }
                // Check maximum block system fee via Native Contract policy
                if (_context.GetExpectedBlockSystemFee() > _dbftSettings.MaxBlockSystemFee)
                {
                    Log($"Rejected block: {_context.Block.Index} The system fee exceed the policy", LogLevel.Warning);
                    RequestChangeView(ChangeViewReason.BlockRejectedByPolicy);
                    return false;
                }

                // Timeout extension due to prepare response sent
                // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
                ExtendTimerByFactor(2);

                Log($"Sending {nameof(PrepareResponse)}");
                _localNode.Tell(new LocalNode.SendDirectly { Inventory = _context.MakePrepareResponse() });
                CheckPreparations();
            }
            return true;
        }

        private void CheckCommits()
        {
            if (_context.CommitPayloads.Count(p => _context.GetMessage(p)?.ViewNumber == _context.ViewNumber) >= _context.M && _context.TransactionHashes.All(p => _context.Transactions.ContainsKey(p)))
            {
                _blockReceivedIndex = _context.Block.Index;
                _blockReceivedTime = TimeProvider.Current.UtcNow;
                Block block = _context.CreateBlock();
                Log($"Sending {nameof(Block)}: height={block.Index} hash={block.Hash} tx={block.Transactions.Length}");
                _blockchain.Tell(block);
            }
        }

        private void CheckExpectedView(byte viewNumber)
        {
            if (_context.ViewNumber >= viewNumber) return;
            var messages = _context.ChangeViewPayloads.Select(p => _context.GetMessage<ChangeView>(p)).ToArray();
            // if there are `M` change view payloads with NewViewNumber greater than viewNumber, then, it is safe to move
            if (messages.Count(p => p != null && p.NewViewNumber >= viewNumber) >= _context.M)
            {
                if (!_context.WatchOnly)
                {
                    ChangeView message = messages[_context.MyIndex];
                    // Communicate the network about my agreement to move to `viewNumber`
                    // if my last change view payload, `message`, has NewViewNumber lower than current view to change
                    if (message is null || message.NewViewNumber < viewNumber)
                        _localNode.Tell(new LocalNode.SendDirectly { Inventory = _context.MakeChangeView(ChangeViewReason.ChangeAgreement) });
                }
                InitializeConsensus(viewNumber);
            }
        }

        private void CheckPreparations()
        {
            if (_context.PreparationPayloads.Count(p => p != null) >= _context.M && _context.TransactionHashes.All(p => _context.Transactions.ContainsKey(p)))
            {
                ExtensiblePayload payload = _context.MakeCommit();
                Log($"Sending {nameof(Commit)}");
                _context.Save();
                _localNode.Tell(new LocalNode.SendDirectly { Inventory = payload });
                // Set timer, so we will resend the commit in case of a networking issue
                ChangeTimer(TimeSpan.FromMilliseconds(_neoSystem.Settings.MillisecondsPerBlock));
                CheckCommits();
            }
        }
    }
}
