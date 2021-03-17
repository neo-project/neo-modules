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
using System.IO;
using System.Linq;
using static Neo.Ledger.Blockchain;

namespace Neo.Consensus
{
    class ConsensusService : UntypedActor
    {
        public class Start { }
        private class Timer { public uint Height; public byte ViewNumber; }

        private readonly ConsensusContext context;
        private readonly IActorRef localNode;
        private readonly IActorRef taskManager;
        private readonly IActorRef blockchain;
        private ICancelable timer_token;
        private DateTime block_received_time;
        private uint block_received_index;
        private bool started = false;

        /// <summary>
        /// This will record the information from last scheduled timer
        /// </summary>
        private DateTime clock_started = TimeProvider.Current.UtcNow;
        private TimeSpan expected_delay = TimeSpan.Zero;

        /// <summary>
        /// This will be cleared every block (so it will not grow out of control, but is used to prevent repeatedly
        /// responding to the same message.
        /// </summary>
        private readonly HashSet<UInt256> knownHashes = new HashSet<UInt256>();
        /// <summary>
        /// This variable is only true during OnRecoveryMessageReceived
        /// </summary>
        private bool isRecovering = false;
        private readonly Settings dbftSettings;
        private readonly NeoSystem neoSystem;

        public ConsensusService(NeoSystem neoSystem, Settings settings, Wallet wallet)
            : this(neoSystem, settings, new ConsensusContext(neoSystem, settings, wallet))
        {
        }

        internal ConsensusService(NeoSystem neoSystem, Settings settings, ConsensusContext context)
        {
            this.neoSystem = neoSystem;
            localNode = neoSystem.LocalNode;
            taskManager = neoSystem.TaskManager;
            blockchain = neoSystem.Blockchain;
            dbftSettings = settings;
            this.context = context;
            Context.System.EventStream.Subscribe(Self, typeof(Blockchain.PersistCompleted));
            Context.System.EventStream.Subscribe(Self, typeof(Blockchain.RelayResult));
        }

        private bool AddTransaction(Transaction tx, bool verify)
        {
            if (verify)
            {
                VerifyResult result = tx.Verify(neoSystem.Settings, context.Snapshot, context.VerificationContext);
                if (result != VerifyResult.Succeed)
                {
                    Log($"Rejected tx: {tx.Hash}, {result}{Environment.NewLine}{tx.ToArray().ToHexString()}", LogLevel.Warning);
                    RequestChangeView(result == VerifyResult.PolicyFail ? ChangeViewReason.TxRejectedByPolicy : ChangeViewReason.TxInvalid);
                    return false;
                }
            }
            context.Transactions[tx.Hash] = tx;
            context.VerificationContext.AddTransaction(tx);
            return CheckPrepareResponse();
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

        private void ChangeTimer(TimeSpan delay)
        {
            clock_started = TimeProvider.Current.UtcNow;
            expected_delay = delay;
            timer_token.CancelIfNotNull();
            timer_token = Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new Timer
            {
                Height = context.Block.Index,
                ViewNumber = context.ViewNumber
            }, ActorRefs.NoSender);
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

        private void InitializeConsensus(byte viewNumber)
        {
            context.Reset(viewNumber);
            if (viewNumber > 0)
                Log($"View changed: view={viewNumber} primary={context.Validators[context.GetPrimaryIndex((byte)(viewNumber - 1u))]}", LogLevel.Warning);
            Log($"Initialize: height={context.Block.Index} view={viewNumber} index={context.MyIndex} role={(context.IsPrimary ? "Primary" : context.WatchOnly ? "WatchOnly" : "Backup")}");
            if (context.WatchOnly) return;
            if (context.IsPrimary)
            {
                if (isRecovering)
                {
                    ChangeTimer(TimeSpan.FromMilliseconds(neoSystem.Settings.MillisecondsPerBlock << (viewNumber + 1)));
                }
                else
                {
                    TimeSpan span = neoSystem.Settings.TimePerBlock;
                    if (block_received_index + 1 == context.Block.Index)
                    {
                        var diff = TimeProvider.Current.UtcNow - block_received_time;
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
                ChangeTimer(TimeSpan.FromMilliseconds(neoSystem.Settings.MillisecondsPerBlock << (viewNumber + 1)));
            }
        }

        private static void Log(string message, LogLevel level = LogLevel.Info)
        {
            Utility.Log(nameof(ConsensusService), level, message);
        }

        private void OnChangeViewReceived(ExtensiblePayload payload, ChangeView message)
        {
            if (message.NewViewNumber <= context.ViewNumber)
                OnRecoveryRequestReceived(payload, message);

            if (context.CommitSent) return;

            var expectedView = context.GetMessage<ChangeView>(context.ChangeViewPayloads[message.ValidatorIndex])?.NewViewNumber ?? 0;
            if (message.NewViewNumber <= expectedView)
                return;

            Log($"{nameof(OnChangeViewReceived)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex} nv={message.NewViewNumber} reason={message.Reason}");
            context.ChangeViewPayloads[message.ValidatorIndex] = payload;
            CheckExpectedView(message.NewViewNumber);
        }

        private void OnCommitReceived(ExtensiblePayload payload, Commit commit)
        {
            ref ExtensiblePayload existingCommitPayload = ref context.CommitPayloads[commit.ValidatorIndex];
            if (existingCommitPayload != null)
            {
                if (existingCommitPayload.Hash != payload.Hash)
                    Log($"Rejected {nameof(Commit)}: height={commit.BlockIndex} index={commit.ValidatorIndex} view={commit.ViewNumber} existingView={context.GetMessage(existingCommitPayload).ViewNumber}", LogLevel.Warning);
                return;
            }

            // Timeout extension: commit has been received with success
            // around 4*15s/M=60.0s/5=12.0s ~ 80% block time (for M=5)
            ExtendTimerByFactor(4);

            if (commit.ViewNumber == context.ViewNumber)
            {
                Log($"{nameof(OnCommitReceived)}: height={commit.BlockIndex} view={commit.ViewNumber} index={commit.ValidatorIndex} nc={context.CountCommitted} nf={context.CountFailed}");

                byte[] hashData = context.EnsureHeader()?.GetSignData(neoSystem.Settings.Magic);
                if (hashData == null)
                {
                    existingCommitPayload = payload;
                }
                else if (Crypto.VerifySignature(hashData, commit.Signature, context.Validators[commit.ValidatorIndex]))
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

        // this function increases existing timer (never decreases) with a value proportional to `maxDelayInBlockTimes`*`Blockchain.MillisecondsPerBlock`
        private void ExtendTimerByFactor(int maxDelayInBlockTimes)
        {
            TimeSpan nextDelay = expected_delay - (TimeProvider.Current.UtcNow - clock_started) + TimeSpan.FromMilliseconds(maxDelayInBlockTimes * neoSystem.Settings.MillisecondsPerBlock / (double)context.M);
            if (!context.WatchOnly && !context.ViewChanging && !context.CommitSent && (nextDelay > TimeSpan.Zero))
                ChangeTimer(nextDelay);
        }

        private void OnConsensusPayload(ExtensiblePayload payload)
        {
            if (context.BlockSent) return;
            ConsensusMessage message;
            try
            {
                message = context.GetMessage(payload);
            }
            catch (FormatException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }
            if (!message.Verify(neoSystem.Settings)) return;
            if (message.BlockIndex != context.Block.Index)
            {
                if (context.Block.Index < message.BlockIndex)
                {
                    Log($"Chain is behind: expected={message.BlockIndex} current={context.Block.Index - 1}", LogLevel.Warning);
                }
                return;
            }
            if (message.ValidatorIndex >= context.Validators.Length) return;
            if (payload.Sender != Contract.CreateSignatureRedeemScript(context.Validators[message.ValidatorIndex]).ToScriptHash()) return;
            context.LastSeenMessage[context.Validators[message.ValidatorIndex]] = message.BlockIndex;
            switch (message)
            {
                case ChangeView view:
                    OnChangeViewReceived(payload, view);
                    break;
                case PrepareRequest request:
                    OnPrepareRequestReceived(payload, request);
                    break;
                case PrepareResponse response:
                    OnPrepareResponseReceived(payload, response);
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

        private void OnPersistCompleted(Block block)
        {
            Log($"Persisted {nameof(Block)}: height={block.Index} hash={block.Hash} tx={block.Transactions.Length}");
            knownHashes.Clear();
            InitializeConsensus(0);
        }

        private void OnRecoveryMessageReceived(RecoveryMessage message)
        {
            // isRecovering is always set to false again after OnRecoveryMessageReceived
            isRecovering = true;
            int validChangeViews = 0, totalChangeViews = 0, validPrepReq = 0, totalPrepReq = 0;
            int validPrepResponses = 0, totalPrepResponses = 0, validCommits = 0, totalCommits = 0;

            Log($"{nameof(OnRecoveryMessageReceived)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex}");
            try
            {
                if (message.ViewNumber > context.ViewNumber)
                {
                    if (context.CommitSent) return;
                    ExtensiblePayload[] changeViewPayloads = message.GetChangeViewPayloads(context);
                    totalChangeViews = changeViewPayloads.Length;
                    foreach (ExtensiblePayload changeViewPayload in changeViewPayloads)
                        if (ReverifyAndProcessPayload(changeViewPayload)) validChangeViews++;
                }
                if (message.ViewNumber == context.ViewNumber && !context.NotAcceptingPayloadsDueToViewChanging && !context.CommitSent)
                {
                    if (!context.RequestSentOrReceived)
                    {
                        ExtensiblePayload prepareRequestPayload = message.GetPrepareRequestPayload(context);
                        if (prepareRequestPayload != null)
                        {
                            totalPrepReq = 1;
                            if (ReverifyAndProcessPayload(prepareRequestPayload)) validPrepReq++;
                        }
                        else if (context.IsPrimary)
                            SendPrepareRequest();
                    }
                    ExtensiblePayload[] prepareResponsePayloads = message.GetPrepareResponsePayloads(context);
                    totalPrepResponses = prepareResponsePayloads.Length;
                    foreach (ExtensiblePayload prepareResponsePayload in prepareResponsePayloads)
                        if (ReverifyAndProcessPayload(prepareResponsePayload)) validPrepResponses++;
                }
                if (message.ViewNumber <= context.ViewNumber)
                {
                    // Ensure we know about all commits from lower view numbers.
                    ExtensiblePayload[] commitPayloads = message.GetCommitPayloadsFromRecoveryMessage(context);
                    totalCommits = commitPayloads.Length;
                    foreach (ExtensiblePayload commitPayload in commitPayloads)
                        if (ReverifyAndProcessPayload(commitPayload)) validCommits++;
                }
            }
            finally
            {
                Log($"Recovery finished: (valid/total) ChgView: {validChangeViews}/{totalChangeViews} PrepReq: {validPrepReq}/{totalPrepReq} PrepResp: {validPrepResponses}/{totalPrepResponses} Commits: {validCommits}/{totalCommits}");
                isRecovering = false;
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
            if (!knownHashes.Add(payload.Hash)) return;

            Log($"{nameof(OnRecoveryRequestReceived)}: height={message.BlockIndex} index={message.ValidatorIndex} view={message.ViewNumber}");
            if (context.WatchOnly) return;
            if (!context.CommitSent)
            {
                bool shouldSendRecovery = false;
                int allowedRecoveryNodeCount = context.F;
                // Limit recoveries to be sent from an upper limit of `f` nodes
                for (int i = 1; i <= allowedRecoveryNodeCount; i++)
                {
                    var chosenIndex = (message.ValidatorIndex + i) % context.Validators.Length;
                    if (chosenIndex != context.MyIndex) continue;
                    shouldSendRecovery = true;
                    break;
                }

                if (!shouldSendRecovery) return;
            }
            localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakeRecoveryMessage() });
        }

        private void OnPrepareRequestReceived(ExtensiblePayload payload, PrepareRequest message)
        {
            if (context.RequestSentOrReceived || context.NotAcceptingPayloadsDueToViewChanging) return;
            if (message.ValidatorIndex != context.Block.PrimaryIndex || message.ViewNumber != context.ViewNumber) return;
            if (message.Version != context.Block.Version || message.PrevHash != context.Block.PrevHash) return;
            if (message.TransactionHashes.Length > neoSystem.Settings.MaxTransactionsPerBlock) return;
            Log($"{nameof(OnPrepareRequestReceived)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex} tx={message.TransactionHashes.Length}");
            if (message.Timestamp <= context.PrevHeader.Timestamp || message.Timestamp > TimeProvider.Current.UtcNow.AddMilliseconds(8 * neoSystem.Settings.MillisecondsPerBlock).ToTimestampMS())
            {
                Log($"Timestamp incorrect: {message.Timestamp}", LogLevel.Warning);
                return;
            }
            if (message.TransactionHashes.Any(p => NativeContract.Ledger.ContainsTransaction(context.Snapshot, p)))
            {
                Log($"Invalid request: transaction already exists", LogLevel.Warning);
                return;
            }

            // Timeout extension: prepare request has been received with success
            // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
            ExtendTimerByFactor(2);

            context.Block.Header.Timestamp = message.Timestamp;
            context.TransactionHashes = message.TransactionHashes;
            context.Transactions = new Dictionary<UInt256, Transaction>();
            context.VerificationContext = new TransactionVerificationContext();
            for (int i = 0; i < context.PreparationPayloads.Length; i++)
                if (context.PreparationPayloads[i] != null)
                    if (!context.GetMessage<PrepareResponse>(context.PreparationPayloads[i]).PreparationHash.Equals(payload.Hash))
                        context.PreparationPayloads[i] = null;
            context.PreparationPayloads[message.ValidatorIndex] = payload;
            byte[] hashData = context.EnsureHeader().GetSignData(neoSystem.Settings.Magic);
            for (int i = 0; i < context.CommitPayloads.Length; i++)
                if (context.GetMessage(context.CommitPayloads[i])?.ViewNumber == context.ViewNumber)
                    if (!Crypto.VerifySignature(hashData, context.GetMessage<Commit>(context.CommitPayloads[i]).Signature, context.Validators[i]))
                        context.CommitPayloads[i] = null;

            if (context.TransactionHashes.Length == 0)
            {
                // There are no tx so we should act like if all the transactions were filled
                CheckPrepareResponse();
                return;
            }

            Dictionary<UInt256, Transaction> mempoolVerified = neoSystem.MemPool.GetVerifiedTransactions().ToDictionary(p => p.Hash);
            List<Transaction> unverified = new List<Transaction>();
            foreach (UInt256 hash in context.TransactionHashes)
            {
                if (mempoolVerified.TryGetValue(hash, out Transaction tx))
                {
                    if (!AddTransaction(tx, false))
                        return;
                }
                else
                {
                    if (neoSystem.MemPool.TryGetValue(hash, out tx))
                        unverified.Add(tx);
                }
            }
            foreach (Transaction tx in unverified)
                if (!AddTransaction(tx, true))
                    return;
            if (context.Transactions.Count < context.TransactionHashes.Length)
            {
                UInt256[] hashes = context.TransactionHashes.Where(i => !context.Transactions.ContainsKey(i)).ToArray();
                taskManager.Tell(new TaskManager.RestartTasks
                {
                    Payload = InvPayload.Create(InventoryType.TX, hashes)
                });
            }
        }

        private void OnPrepareResponseReceived(ExtensiblePayload payload, PrepareResponse message)
        {
            if (message.ViewNumber != context.ViewNumber) return;
            if (context.PreparationPayloads[message.ValidatorIndex] != null || context.NotAcceptingPayloadsDueToViewChanging) return;
            if (context.PreparationPayloads[context.Block.PrimaryIndex] != null && !message.PreparationHash.Equals(context.PreparationPayloads[context.Block.PrimaryIndex].Hash))
                return;

            // Timeout extension: prepare response has been received with success
            // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
            ExtendTimerByFactor(2);

            Log($"{nameof(OnPrepareResponseReceived)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex}");
            context.PreparationPayloads[message.ValidatorIndex] = payload;
            if (context.WatchOnly || context.CommitSent) return;
            if (context.RequestSentOrReceived)
                CheckPreparations();
        }

        protected override void OnReceive(object message)
        {
            if (message is Start)
            {
                if (started) return;
                OnStart();
            }
            else
            {
                if (!started) return;
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

        private void RequestRecovery()
        {
            Log($"Sending {nameof(RecoveryRequest)}: height={context.Block.Index} view={context.ViewNumber} nc={context.CountCommitted} nf={context.CountFailed}");
            localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakeRecoveryRequest() });
        }

        private void OnStart()
        {
            Log("OnStart");
            started = true;
            if (!dbftSettings.IgnoreRecoveryLogs && context.Load())
            {
                if (context.Transactions != null)
                {
                    blockchain.Ask<Blockchain.FillCompleted>(new Blockchain.FillMemoryPool
                    {
                        Transactions = context.Transactions.Values
                    }).Wait();
                }
                if (context.CommitSent)
                {
                    CheckPreparations();
                    return;
                }
            }
            InitializeConsensus(0);
            // Issue a ChangeView with NewViewNumber of 0 to request recovery messages on start-up.
            if (!context.WatchOnly)
                RequestRecovery();
        }

        private void OnTimer(Timer timer)
        {
            if (context.WatchOnly || context.BlockSent) return;
            if (timer.Height != context.Block.Index || timer.ViewNumber != context.ViewNumber) return;
            if (context.IsPrimary && !context.RequestSentOrReceived)
            {
                SendPrepareRequest();
            }
            else if ((context.IsPrimary && context.RequestSentOrReceived) || context.IsBackup)
            {
                if (context.CommitSent)
                {
                    // Re-send commit periodically by sending recover message in case of a network issue.
                    Log($"Sending {nameof(RecoveryMessage)} to resend {nameof(Commit)}");
                    localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakeRecoveryMessage() });
                    ChangeTimer(TimeSpan.FromMilliseconds(neoSystem.Settings.MillisecondsPerBlock << 1));
                }
                else
                {
                    var reason = ChangeViewReason.Timeout;

                    if (context.Block != null && context.TransactionHashes?.Length > context.Transactions?.Count)
                    {
                        reason = ChangeViewReason.TxNotFound;
                    }

                    RequestChangeView(reason);
                }
            }
        }

        private void OnTransaction(Transaction transaction)
        {
            if (!context.IsBackup || context.NotAcceptingPayloadsDueToViewChanging || !context.RequestSentOrReceived || context.ResponseSent || context.BlockSent)
                return;
            if (context.Transactions.ContainsKey(transaction.Hash)) return;
            if (!context.TransactionHashes.Contains(transaction.Hash)) return;
            AddTransaction(transaction, true);
        }

        protected override void PostStop()
        {
            Log("OnStop");
            started = false;
            Context.System.EventStream.Unsubscribe(Self);
            context.Dispose();
            base.PostStop();
        }

        public static Props Props(NeoSystem neoSystem, Settings dbftSettings, Wallet wallet)
        {
            return Akka.Actor.Props.Create(() => new ConsensusService(neoSystem, dbftSettings, wallet));
        }

        private void RequestChangeView(ChangeViewReason reason)
        {
            if (context.WatchOnly) return;
            // Request for next view is always one view more than the current context.ViewNumber
            // Nodes will not contribute for changing to a view higher than (context.ViewNumber+1), unless they are recovered
            // The latter may happen by nodes in higher views with, at least, `M` proofs
            byte expectedView = context.ViewNumber;
            expectedView++;
            ChangeTimer(TimeSpan.FromMilliseconds(neoSystem.Settings.MillisecondsPerBlock << (expectedView + 1)));
            if ((context.CountCommitted + context.CountFailed) > context.F)
            {
                RequestRecovery();
            }
            else
            {
                Log($"Sending {nameof(ChangeView)}: height={context.Block.Index} view={context.ViewNumber} nv={expectedView} nc={context.CountCommitted} nf={context.CountFailed} reason={reason}");
                localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakeChangeView(reason) });
                CheckExpectedView(expectedView);
            }
        }

        private bool ReverifyAndProcessPayload(ExtensiblePayload payload)
        {
            RelayResult relayResult = blockchain.Ask<RelayResult>(new Blockchain.Reverify { Inventories = new IInventory[] { payload } }).Result;
            if (relayResult.Result != VerifyResult.Succeed) return false;
            OnConsensusPayload(payload);
            return true;
        }

        private void SendPrepareRequest()
        {
            Log($"Sending {nameof(PrepareRequest)}: height={context.Block.Index} view={context.ViewNumber}");
            localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakePrepareRequest() });

            if (context.Validators.Length == 1)
                CheckPreparations();

            if (context.TransactionHashes.Length > 0)
            {
                foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, context.TransactionHashes))
                    localNode.Tell(Message.Create(MessageCommand.Inv, payload));
            }
            ChangeTimer(TimeSpan.FromMilliseconds((neoSystem.Settings.MillisecondsPerBlock << (context.ViewNumber + 1)) - (context.ViewNumber == 0 ? neoSystem.Settings.MillisecondsPerBlock : 0)));
        }
    }
}
