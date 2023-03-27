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
using Neo.Cryptography;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Consensus
{
    partial class ConsensusService
    {
        private void OnConsensusPayload(ExtensiblePayload payload)
        {
            if (_context.BlockSent) return;
            ConsensusMessage message;
            try
            {
                message = _context.GetMessage(payload);
            }
            catch (Exception ex)
            {
                Utility.Log(nameof(ConsensusService), LogLevel.Debug, ex.ToString());
                return;
            }

            if (!message.Verify(_neoSystem.Settings)) return;
            if (message.BlockIndex != _context.Block.Index)
            {
                if (_context.Block.Index < message.BlockIndex)
                {
                    Log($"Chain is behind: expected={message.BlockIndex} current={_context.Block.Index - 1}", LogLevel.Warning);
                }
                return;
            }
            if (message.ValidatorIndex >= _context.Validators.Length) return;
            if (payload.Sender != Contract.CreateSignatureRedeemScript(_context.Validators[message.ValidatorIndex]).ToScriptHash()) return;
            _context.LastSeenMessage[_context.Validators[message.ValidatorIndex]] = message.BlockIndex;
            switch (message)
            {
                case PrepareRequest request:
                    OnPrepareRequestReceived(payload, request);
                    break;
                case PrepareResponse response:
                    OnPrepareResponseReceived(payload, response);
                    break;
                case ChangeView view:
                    OnChangeViewReceived(payload, view);
                    break;
                case Commit commit:
                    OnCommitReceived(payload, commit);
                    break;
                case RecoveryRequest request:
                    OnRecoveryRequestReceived(payload, request);
                    break;
                case RecoveryMessage recovery:
                    OnRecoveryMessageReceived(recovery);
                    break;
            }
        }

        private void OnPrepareRequestReceived(ExtensiblePayload payload, PrepareRequest message)
        {
            if (_context.RequestSentOrReceived || _context.NotAcceptingPayloadsDueToViewChanging) return;
            if (message.ValidatorIndex != _context.Block.PrimaryIndex || message.ViewNumber != _context.ViewNumber) return;
            if (message.Version != _context.Block.Version || message.PrevHash != _context.Block.PrevHash) return;
            if (message.TransactionHashes.Length > _neoSystem.Settings.MaxTransactionsPerBlock) return;
            Log($"{nameof(OnPrepareRequestReceived)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex} tx={message.TransactionHashes.Length}");
            if (message.Timestamp <= _context.PrevHeader.Timestamp || message.Timestamp > TimeProvider.Current.UtcNow.AddMilliseconds(8 * _neoSystem.Settings.MillisecondsPerBlock).ToTimestampMS())
            {
                Log($"Timestamp incorrect: {message.Timestamp}", LogLevel.Warning);
                return;
            }

            if (message.TransactionHashes.Any(p => NativeContract.Ledger.ContainsTransaction(_context.Snapshot, p)))
            {
                Log($"Invalid request: transaction already exists", LogLevel.Warning);
                return;
            }

            // Timeout extension: prepare request has been received with success
            // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
            ExtendTimerByFactor(2);

            _context.Block.Header.Timestamp = message.Timestamp;
            _context.Block.Header.Nonce = message.Nonce;
            _context.TransactionHashes = message.TransactionHashes;

            _context.Transactions = new Dictionary<UInt256, Transaction>();
            _context.VerificationContext = new TransactionVerificationContext();
            for (int i = 0; i < _context.PreparationPayloads.Length; i++)
                if (_context.PreparationPayloads[i] != null)
                    if (!_context.GetMessage<PrepareResponse>(_context.PreparationPayloads[i]).PreparationHash.Equals(payload.Hash))
                        _context.PreparationPayloads[i] = null;
            _context.PreparationPayloads[message.ValidatorIndex] = payload;
            byte[] hashData = _context.EnsureHeader().GetSignData(_neoSystem.Settings.Network);
            for (int i = 0; i < _context.CommitPayloads.Length; i++)
                if (_context.GetMessage(_context.CommitPayloads[i])?.ViewNumber == _context.ViewNumber)
                    if (!Crypto.VerifySignature(hashData, _context.GetMessage<Commit>(_context.CommitPayloads[i]).Signature.Span, _context.Validators[i]))
                        _context.CommitPayloads[i] = null;

            if (_context.TransactionHashes.Length == 0)
            {
                // There are no tx so we should act like if all the transactions were filled
                CheckPrepareResponse();
                return;
            }

            Dictionary<UInt256, Transaction> mempoolVerified = _neoSystem.MemPool.GetVerifiedTransactions().ToDictionary(p => p.Hash);
            List<Transaction> unverified = new List<Transaction>();
            foreach (UInt256 hash in _context.TransactionHashes)
            {
                if (mempoolVerified.TryGetValue(hash, out Transaction tx))
                {
                    if (!AddTransaction(tx, false))
                        return;
                }
                else
                {
                    if (_neoSystem.MemPool.TryGetValue(hash, out tx))
                        unverified.Add(tx);
                }
            }
            foreach (Transaction tx in unverified)
                if (!AddTransaction(tx, true))
                    return;
            if (_context.Transactions.Count < _context.TransactionHashes.Length)
            {
                UInt256[] hashes = _context.TransactionHashes.Where(i => !_context.Transactions.ContainsKey(i)).ToArray();
                _taskManager.Tell(new TaskManager.RestartTasks
                {
                    Payload = InvPayload.Create(InventoryType.TX, hashes)
                });
            }
        }

        private void OnPrepareResponseReceived(ExtensiblePayload payload, PrepareResponse message)
        {
            if (message.ViewNumber != _context.ViewNumber) return;
            if (_context.PreparationPayloads[message.ValidatorIndex] != null || _context.NotAcceptingPayloadsDueToViewChanging) return;
            if (_context.PreparationPayloads[_context.Block.PrimaryIndex] != null && !message.PreparationHash.Equals(_context.PreparationPayloads[_context.Block.PrimaryIndex].Hash))
                return;

            // Timeout extension: prepare response has been received with success
            // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
            ExtendTimerByFactor(2);

            Log($"{nameof(OnPrepareResponseReceived)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex}");
            _context.PreparationPayloads[message.ValidatorIndex] = payload;
            if (_context.WatchOnly || _context.CommitSent) return;
            if (_context.RequestSentOrReceived)
                CheckPreparations();
        }

        private void OnChangeViewReceived(ExtensiblePayload payload, ChangeView message)
        {
            if (message.NewViewNumber <= _context.ViewNumber)
                OnRecoveryRequestReceived(payload, message);

            if (_context.CommitSent) return;

            var expectedView = _context.GetMessage<ChangeView>(_context.ChangeViewPayloads[message.ValidatorIndex])?.NewViewNumber ?? 0;
            if (message.NewViewNumber <= expectedView)
                return;

            Log($"{nameof(OnChangeViewReceived)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex} nv={message.NewViewNumber} reason={message.Reason}");
            _context.ChangeViewPayloads[message.ValidatorIndex] = payload;
            CheckExpectedView(message.NewViewNumber);
        }

        private void OnCommitReceived(ExtensiblePayload payload, Commit commit)
        {
            ref ExtensiblePayload existingCommitPayload = ref _context.CommitPayloads[commit.ValidatorIndex];
            if (existingCommitPayload != null)
            {
                if (existingCommitPayload.Hash != payload.Hash)
                    Log($"Rejected {nameof(Commit)}: height={commit.BlockIndex} index={commit.ValidatorIndex} view={commit.ViewNumber} existingView={_context.GetMessage(existingCommitPayload).ViewNumber}", LogLevel.Warning);
                return;
            }

            if (commit.ViewNumber == _context.ViewNumber)
            {
                // Timeout extension: commit has been received with success
                // around 4*15s/M=60.0s/5=12.0s ~ 80% block time (for M=5)
                ExtendTimerByFactor(4);

                Log($"{nameof(OnCommitReceived)}: height={commit.BlockIndex} view={commit.ViewNumber} index={commit.ValidatorIndex} nc={_context.CountCommitted} nf={_context.CountFailed}");

                byte[] hashData = _context.EnsureHeader()?.GetSignData(_neoSystem.Settings.Network);
                if (hashData == null)
                {
                    existingCommitPayload = payload;
                }
                else if (Crypto.VerifySignature(hashData, commit.Signature.Span, _context.Validators[commit.ValidatorIndex]))
                {
                    existingCommitPayload = payload;
                    CheckCommits();
                }
                return;
            }
            else
            {
                // Receiving commit from another view
                existingCommitPayload = payload;
            }
        }

        private void OnRecoveryMessageReceived(RecoveryMessage message)
        {
            // isRecovering is always set to false again after OnRecoveryMessageReceived
            _isRecovering = true;
            int validChangeViews = 0, totalChangeViews = 0, validPrepReq = 0, totalPrepReq = 0;
            int validPrepResponses = 0, totalPrepResponses = 0, validCommits = 0, totalCommits = 0;

            Log($"{nameof(OnRecoveryMessageReceived)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex}");
            try
            {
                if (message.ViewNumber > _context.ViewNumber)
                {
                    if (_context.CommitSent) return;
                    ExtensiblePayload[] changeViewPayloads = message.GetChangeViewPayloads(_context);
                    totalChangeViews = changeViewPayloads.Length;
                    foreach (ExtensiblePayload changeViewPayload in changeViewPayloads)
                        if (ReverifyAndProcessPayload(changeViewPayload)) validChangeViews++;
                }
                if (message.ViewNumber == _context.ViewNumber && !_context.NotAcceptingPayloadsDueToViewChanging && !_context.CommitSent)
                {
                    if (!_context.RequestSentOrReceived)
                    {
                        ExtensiblePayload prepareRequestPayload = message.GetPrepareRequestPayload(_context);
                        if (prepareRequestPayload != null)
                        {
                            totalPrepReq = 1;
                            if (ReverifyAndProcessPayload(prepareRequestPayload)) validPrepReq++;
                        }
                        else if (_context.IsPrimary)
                            SendPrepareRequest();
                    }
                    ExtensiblePayload[] prepareResponsePayloads = message.GetPrepareResponsePayloads(_context);
                    totalPrepResponses = prepareResponsePayloads.Length;
                    foreach (ExtensiblePayload prepareResponsePayload in prepareResponsePayloads)
                        if (ReverifyAndProcessPayload(prepareResponsePayload)) validPrepResponses++;
                }
                if (message.ViewNumber <= _context.ViewNumber)
                {
                    // Ensure we know about all commits from lower view numbers.
                    ExtensiblePayload[] commitPayloads = message.GetCommitPayloadsFromRecoveryMessage(_context);
                    totalCommits = commitPayloads.Length;
                    foreach (ExtensiblePayload commitPayload in commitPayloads)
                        if (ReverifyAndProcessPayload(commitPayload)) validCommits++;
                }
            }
            finally
            {
                Log($"Recovery finished: (valid/total) ChgView: {validChangeViews}/{totalChangeViews} PrepReq: {validPrepReq}/{totalPrepReq} PrepResp: {validPrepResponses}/{totalPrepResponses} Commits: {validCommits}/{totalCommits}");
                _isRecovering = false;
            }
        }

        private void OnRecoveryRequestReceived(ExtensiblePayload payload, ConsensusMessage message)
        {
            // We keep track of the payload hashes received in this block, and don't respond with recovery
            // in response to the same payload that we already responded to previously.
            // ChangeView messages include a Timestamp when the change view is sent, thus if a node restarts
            // and issues a change view for the same view, it will have a different hash and will correctly respond
            // again; however replay attacks of the ChangeView message from arbitrary nodes will not trigger an
            // additional recovery message response.
            if (!_knownHashes.Add(payload.Hash)) return;

            Log($"{nameof(OnRecoveryRequestReceived)}: height={message.BlockIndex} index={message.ValidatorIndex} view={message.ViewNumber}");
            if (_context.WatchOnly) return;
            if (!_context.CommitSent)
            {
                bool shouldSendRecovery = false;
                int allowedRecoveryNodeCount = _context.F + 1;
                // Limit recoveries to be sent from an upper limit of `f + 1` nodes
                for (int i = 1; i <= allowedRecoveryNodeCount; i++)
                {
                    var chosenIndex = (message.ValidatorIndex + i) % _context.Validators.Length;
                    if (chosenIndex != _context.MyIndex) continue;
                    shouldSendRecovery = true;
                    break;
                }

                if (!shouldSendRecovery) return;
            }
            _localNode.Tell(new LocalNode.SendDirectly { Inventory = _context.MakeRecoveryMessage() });
        }
    }
}
