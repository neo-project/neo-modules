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
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Ledger.Blockchain;

namespace Neo.Consensus
{
    partial class ConsensusService : UntypedActor
    {
        public class Start { }
        private class Timer { public uint Height; public byte ViewNumber; }

        private readonly ConsensusContext _context;
        private readonly IActorRef _localNode;
        private readonly IActorRef _taskManager;
        private readonly IActorRef _blockchain;
        private ICancelable _timerToken;
        private DateTime _blockReceivedTime;
        private uint _blockReceivedIndex;
        private bool _started = false;

        /// <summary>
        /// This will record the information from last scheduled timer
        /// </summary>
        private DateTime clock_started = TimeProvider.Current.UtcNow;
        private TimeSpan expected_delay = TimeSpan.Zero;

        /// <summary>
        /// This will be cleared every block (so it will not grow out of control, but is used to prevent repeatedly
        /// responding to the same message.
        /// </summary>
        private readonly HashSet<UInt256> _knownHashes = new HashSet<UInt256>();
        /// <summary>
        /// This variable is only true during OnRecoveryMessageReceived
        /// </summary>
        private bool _isRecovering = false;
        private readonly Settings _dbftSettings;
        private readonly NeoSystem _neoSystem;

        public ConsensusService(NeoSystem neoSystem, Settings settings, Wallet wallet)
            : this(neoSystem, settings, new ConsensusContext(neoSystem, settings, wallet)) { }

        internal ConsensusService(NeoSystem neoSystem, Settings settings, ConsensusContext context)
        {
            this._neoSystem = neoSystem;
            _localNode = neoSystem.LocalNode;
            _taskManager = neoSystem.TaskManager;
            _blockchain = neoSystem.Blockchain;
            _dbftSettings = settings;
            this._context = context;
            Context.System.EventStream.Subscribe(Self, typeof(Blockchain.PersistCompleted));
            Context.System.EventStream.Subscribe(Self, typeof(Blockchain.RelayResult));
        }

        private void OnPersistCompleted(Block block)
        {
            Log($"Persisted {nameof(Block)}: height={block.Index} hash={block.Hash} tx={block.Transactions.Length} nonce={block.Nonce}");
            _knownHashes.Clear();
            InitializeConsensus(0);
        }

        private void InitializeConsensus(byte viewNumber)
        {
            _context.Reset(viewNumber);
            if (viewNumber > 0)
                Log($"View changed: view={viewNumber} primary={_context.Validators[_context.GetPrimaryIndex((byte)(viewNumber - 1u))]}", LogLevel.Warning);
            Log($"Initialize: height={_context.Block.Index} view={viewNumber} index={_context.MyIndex} role={(_context.IsPrimary ? "Primary" : _context.WatchOnly ? "WatchOnly" : "Backup")}");
            if (_context.WatchOnly) return;
            if (_context.IsPrimary)
            {
                if (_isRecovering)
                {
                    ChangeTimer(TimeSpan.FromMilliseconds(_neoSystem.Settings.MillisecondsPerBlock << (viewNumber + 1)));
                }
                else
                {
                    TimeSpan span = _neoSystem.Settings.TimePerBlock;
                    if (_blockReceivedIndex + 1 == _context.Block.Index)
                    {
                        var diff = TimeProvider.Current.UtcNow - _blockReceivedTime;
                        if (diff >= span)
                            span = TimeSpan.Zero;
                        else
                            span -= diff;
                    }
                    ChangeTimer(span);
                }
            }
            else
            {
                ChangeTimer(TimeSpan.FromMilliseconds(_neoSystem.Settings.MillisecondsPerBlock << (viewNumber + 1)));
            }
        }

        protected override void OnReceive(object message)
        {
            if (message is Start)
            {
                if (_started) return;
                OnStart();
            }
            else
            {
                if (!_started) return;
                switch (message)
                {
                    case Timer timer:
                        OnTimer(timer);
                        break;
                    case Transaction transaction:
                        OnTransaction(transaction);
                        break;
                    case Blockchain.PersistCompleted completed:
                        OnPersistCompleted(completed.Block);
                        break;
                    case Blockchain.RelayResult rr:
                        if (rr.Result == VerifyResult.Succeed && rr.Inventory is ExtensiblePayload payload && payload.Category == "dBFT")
                            OnConsensusPayload(payload);
                        break;
                }
            }
        }

        private void OnStart()
        {
            Log("OnStart");
            _started = true;
            if (!_dbftSettings.IgnoreRecoveryLogs && _context.Load())
            {
                if (_context.Transactions != null)
                {
                    _blockchain.Ask<Blockchain.FillCompleted>(new Blockchain.FillMemoryPool
                    {
                        Transactions = _context.Transactions.Values
                    }).Wait();
                }
                if (_context.CommitSent)
                {
                    CheckPreparations();
                    return;
                }
            }
            InitializeConsensus(0);
            // Issue a recovery request on start-up in order to possibly catch up with other nodes
            if (!_context.WatchOnly)
                RequestRecovery();
        }

        private void OnTimer(Timer timer)
        {
            if (_context.WatchOnly || _context.BlockSent) return;
            if (timer.Height != _context.Block.Index || timer.ViewNumber != _context.ViewNumber) return;
            if (_context.IsPrimary && !_context.RequestSentOrReceived)
            {
                SendPrepareRequest();
            }
            else if ((_context.IsPrimary && _context.RequestSentOrReceived) || _context.IsBackup)
            {
                if (_context.CommitSent)
                {
                    // Re-send commit periodically by sending recover message in case of a network issue.
                    Log($"Sending {nameof(RecoveryMessage)} to resend {nameof(Commit)}");
                    _localNode.Tell(new LocalNode.SendDirectly { Inventory = _context.MakeRecoveryMessage() });
                    ChangeTimer(TimeSpan.FromMilliseconds(_neoSystem.Settings.MillisecondsPerBlock << 1));
                }
                else
                {
                    var reason = ChangeViewReason.Timeout;

                    if (_context.Block != null && _context.TransactionHashes?.Length > _context.Transactions?.Count)
                    {
                        reason = ChangeViewReason.TxNotFound;
                    }

                    RequestChangeView(reason);
                }
            }
        }

        private void SendPrepareRequest()
        {
            Log($"Sending {nameof(PrepareRequest)}: height={_context.Block.Index} view={_context.ViewNumber}");
            _localNode.Tell(new LocalNode.SendDirectly { Inventory = _context.MakePrepareRequest() });

            if (_context.Validators.Length == 1)
                CheckPreparations();

            if (_context.TransactionHashes.Length > 0)
            {
                foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, _context.TransactionHashes))
                    _localNode.Tell(Message.Create(MessageCommand.Inv, payload));
            }
            ChangeTimer(TimeSpan.FromMilliseconds((_neoSystem.Settings.MillisecondsPerBlock << (_context.ViewNumber + 1)) - (_context.ViewNumber == 0 ? _neoSystem.Settings.MillisecondsPerBlock : 0)));
        }

        private void RequestRecovery()
        {
            Log($"Sending {nameof(RecoveryRequest)}: height={_context.Block.Index} view={_context.ViewNumber} nc={_context.CountCommitted} nf={_context.CountFailed}");
            _localNode.Tell(new LocalNode.SendDirectly { Inventory = _context.MakeRecoveryRequest() });
        }

        private void RequestChangeView(ChangeViewReason reason)
        {
            if (_context.WatchOnly) return;
            // Request for next view is always one view more than the current context.ViewNumber
            // Nodes will not contribute for changing to a view higher than (context.ViewNumber+1), unless they are recovered
            // The latter may happen by nodes in higher views with, at least, `M` proofs
            byte expectedView = _context.ViewNumber;
            expectedView++;
            ChangeTimer(TimeSpan.FromMilliseconds(_neoSystem.Settings.MillisecondsPerBlock << (expectedView + 1)));
            if ((_context.CountCommitted + _context.CountFailed) > _context.F)
            {
                RequestRecovery();
            }
            else
            {
                Log($"Sending {nameof(ChangeView)}: height={_context.Block.Index} view={_context.ViewNumber} nv={expectedView} nc={_context.CountCommitted} nf={_context.CountFailed} reason={reason}");
                _localNode.Tell(new LocalNode.SendDirectly { Inventory = _context.MakeChangeView(reason) });
                CheckExpectedView(expectedView);
            }
        }

        private bool ReverifyAndProcessPayload(ExtensiblePayload payload)
        {
            RelayResult relayResult = _blockchain.Ask<RelayResult>(new Blockchain.Reverify { Inventories = new IInventory[] { payload } }).Result;
            if (relayResult.Result != VerifyResult.Succeed) return false;
            OnConsensusPayload(payload);
            return true;
        }

        private void OnTransaction(Transaction transaction)
        {
            if (!_context.IsBackup || _context.NotAcceptingPayloadsDueToViewChanging || !_context.RequestSentOrReceived || _context.ResponseSent || _context.BlockSent)
                return;
            if (_context.Transactions.ContainsKey(transaction.Hash)) return;
            if (!_context.TransactionHashes.Contains(transaction.Hash)) return;
            AddTransaction(transaction, true);
        }

        private bool AddTransaction(Transaction tx, bool verify)
        {
            if (verify)
            {
                VerifyResult result = tx.Verify(_neoSystem.Settings, _context.Snapshot, _context.VerificationContext);
                if (result != VerifyResult.Succeed)
                {
                    Log($"Rejected tx: {tx.Hash}, {result}{Environment.NewLine}{tx.ToArray().ToHexString()}", LogLevel.Warning);
                    RequestChangeView(result == VerifyResult.PolicyFail ? ChangeViewReason.TxRejectedByPolicy : ChangeViewReason.TxInvalid);
                    return false;
                }
            }
            _context.Transactions[tx.Hash] = tx;
            _context.VerificationContext.AddTransaction(tx);
            return CheckPrepareResponse();
        }

        private void ChangeTimer(TimeSpan delay)
        {
            clock_started = TimeProvider.Current.UtcNow;
            expected_delay = delay;
            _timerToken.CancelIfNotNull();
            _timerToken = Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new Timer
            {
                Height = _context.Block.Index,
                ViewNumber = _context.ViewNumber
            }, ActorRefs.NoSender);
        }

        // this function increases existing timer (never decreases) with a value proportional to `maxDelayInBlockTimes`*`Blockchain.MillisecondsPerBlock`
        private void ExtendTimerByFactor(int maxDelayInBlockTimes)
        {
            TimeSpan nextDelay = expected_delay - (TimeProvider.Current.UtcNow - clock_started) + TimeSpan.FromMilliseconds(maxDelayInBlockTimes * _neoSystem.Settings.MillisecondsPerBlock / (double)_context.M);
            if (!_context.WatchOnly && !_context.ViewChanging && !_context.CommitSent && (nextDelay > TimeSpan.Zero))
                ChangeTimer(nextDelay);
        }

        protected override void PostStop()
        {
            Log("OnStop");
            _started = false;
            Context.System.EventStream.Unsubscribe(Self);
            _context.Dispose();
            base.PostStop();
        }

        public static Props Props(NeoSystem neoSystem, Settings dbftSettings, Wallet wallet)
        {
            return Akka.Actor.Props.Create(() => new ConsensusService(neoSystem, dbftSettings, wallet));
        }

        private static void Log(string message, LogLevel level = LogLevel.Info)
        {
            Utility.Log(nameof(ConsensusService), level, message);
        }
    }
}
