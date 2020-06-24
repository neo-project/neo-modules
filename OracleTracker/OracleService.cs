using Akka.Actor;
using Neo;
using Neo.Cryptography.ECC;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Oracle.Protocols.Https;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.SmartContract.Native.Tokens;
using Neo.VM;
using Neo.Wallets;
using OracleTracker.Protocols;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace OracleTracker
{
    class OracleService
    {
        private (Contract Contract, KeyPair Key)[] _accounts;
        private readonly IActorRef _blockChain;

        private long _isStarted = 0;
        private CancellationTokenSource _cancel;
        private Task[] _oracleTasks;
        private static System.Timers.Timer _gcTimer;
        private static readonly TimeSpan TimeoutInterval = TimeSpan.FromMinutes(5);

        private readonly Func<SnapshotView> _snapshotFactory;
        private Func<OracleRequest, OracleResponse> Protocols { get; }
        private static IOracleProtocol HTTPSProtocol { get; } = new OracleHttpProtocol();

        private readonly SortedBlockingCollection<UInt256, OracleTask> _processingQueue;
        private readonly SortedConcurrentDictionary<UInt256, OracleTask> _pendingQueue;

        internal class OracleTask
        {
            public readonly DateTime Timestamp;
            public UInt256 requestTxHash;
            public OracleRequest request;
            public OracleResponse response;
            public ResponseCollection responseItems;
            private Object locker = new Object();

            public OracleTask(UInt256 requestTxHash, OracleRequest request = null, OracleResponse response = null)
            {
                this.requestTxHash = requestTxHash;
                this.request = request;
                this.response = response;
                this.responseItems = new ResponseCollection();
                this.Timestamp = TimeProvider.Current.UtcNow;
            }

            public bool UpdateTaskState(OracleRequest request = null, OracleResponse response = null)
            {
                if (request != null) this.request = request;
                if (response != null) this.response = response;
                return true;
            }

            public bool AddResponseItem(Contract contract, ECPoint[] publicKeys, ResponseItem item, IActorRef _blockChain, SortedConcurrentDictionary<UInt256, OracleTask> _pendingQueue)
            {
                lock (locker)
                {
                    responseItems.RemoveOutOfDateResponseItem(publicKeys);
                    responseItems.Add(item);

                    var mine_responseItem = responseItems.Where(p => p.IsMine).FirstOrDefault();
                    if (mine_responseItem is null) return true;
                    ContractParametersContext responseTransactionContext = new ContractParametersContext(mine_responseItem.Tx);

                    foreach (var responseItem in responseItems)
                    {
                        responseTransactionContext.AddSignature(contract, responseItem.OraclePub, responseItem.Signature);
                    }
                    if (responseTransactionContext.Completed)
                    {
                        mine_responseItem.Tx.Witnesses = responseTransactionContext.GetWitnesses();
                        Log($"Send response tx: responseTx={mine_responseItem.Tx.Hash}");
                        _pendingQueue.TryRemove(mine_responseItem.TransactionRequestHash, out _);
                        _blockChain.Tell(new Blockchain.RelayResult { Inventory = mine_responseItem.Tx });
                    }
                }
                return true;
            }
        }

        internal class ResponseItem
        {
            public readonly Transaction Tx;
            public readonly OraclePayload Payload;
            public readonly OracleResponseSignature Data;
            public readonly DateTime Timestamp;
            public ECPoint OraclePub => Payload.OraclePub;
            public byte[] Signature => Data.Signature;
            public UInt256 TransactionResponseHash => Data.TransactionResponseHash;
            public UInt256 TransactionRequestHash => Data.TransactionRequestHash;
            public bool IsMine => Tx != null;

            public ResponseItem(OraclePayload payload, Transaction responseTx = null)
            {
                this.Tx = responseTx;
                this.Payload = payload;
                this.Data = payload.OracleSignature;
                this.Timestamp = TimeProvider.Current.UtcNow;
            }

            public bool Verify(StoreView snapshot)
            {
                return Payload.Verify(snapshot);
            }
        }

        internal class ResponseCollection : IEnumerable<ResponseItem>
        {
            private readonly Dictionary<ECPoint, ResponseItem> _items = new Dictionary<ECPoint, ResponseItem>();
            public int Count => _items.Count;

            public bool Add(ResponseItem item)
            {
                if (_items.TryGetValue(item.OraclePub, out var prev))
                {
                    if (prev.Timestamp > item.Timestamp) return false;
                    _items[item.OraclePub] = item;
                    return true;
                }
                if (_items.TryAdd(item.OraclePub, item)) return true;
                return false;
            }

            public bool RemoveOutOfDateResponseItem(ECPoint[] publicKeys)
            {
                List<ECPoint> temp = new List<ECPoint>();
                foreach (var item in _items)
                {
                    if (!publicKeys.Contains(item.Key))
                    {
                        temp.Add(item.Key);
                    }
                }
                foreach (var e in temp)
                {
                    _items.Remove(e, out _);
                }
                return true;
            }

            public IEnumerator<ResponseItem> GetEnumerator()
            {
                return (IEnumerator<ResponseItem>)_items.Select(u => u.Value).ToArray().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        public OracleService(IActorRef blockChain, int capacity)
        {
            Protocols = Process;
            _accounts = new (Contract Contract, KeyPair Key)[0];
            _snapshotFactory = new Func<SnapshotView>(() => Blockchain.Singleton.GetSnapshot());
            _blockChain = blockChain;
            _processingQueue = new SortedBlockingCollection<UInt256, OracleTask>(Comparer<KeyValuePair<UInt256, OracleTask>>.Create(SortTask), capacity);
            _pendingQueue = new SortedConcurrentDictionary<UInt256, OracleTask>(Comparer<KeyValuePair<UInt256, OracleTask>>.Create(SortTask), capacity);
        }

        public bool Start(Wallet wallet, byte numberOfTasks = 4)
        {
            if (Interlocked.Exchange(ref _isStarted, 1) != 0) return false;
            if (numberOfTasks == 0) throw new ArgumentException("The task count must be greater than 0");
            using var snapshot = _snapshotFactory();
            var oracles = NativeContract.Oracle.GetOracleValidators(snapshot)
                .Select(u => Contract.CreateSignatureRedeemScript(u).ToScriptHash());

            _accounts = wallet?.GetAccounts()
                .Where(u => u.HasKey && !u.Lock && oracles.Contains(u.ScriptHash))
                .Select(u => (u.Contract, u.GetKey()))
                .ToArray();

            if (_accounts.Length == 0)
            {
                throw new ArgumentException("The wallet doesn't have any oracle accounts");
            }

            // Create tasks
            Log($"OnStart: tasks={numberOfTasks}");

            _cancel = new CancellationTokenSource();
            _oracleTasks = new Task[numberOfTasks];

            for (int x = 0; x < _oracleTasks.Length; x++)
            {
                _oracleTasks[x] = new Task(() =>
                {
                    foreach (var tx in _processingQueue.GetConsumingEnumerable(_cancel.Token))
                    {
                        ProcessRequest(tx);
                    }
                },
                _cancel.Token);
            }

            _gcTimer = new System.Timers.Timer();
            _gcTimer.Elapsed += new ElapsedEventHandler(CleanOutOfDateOracleTask);
            _gcTimer.Interval = 5000;
            _gcTimer.AutoReset = true;
            _gcTimer.Enabled = true;
            // Start tasks
            foreach (var task in _oracleTasks) task.Start();
            return true;
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _isStarted, 0) != 1) return;
            Log("OnStop");
            _cancel.Cancel();
            for (int x = 0; x < _oracleTasks.Length; x++)
            {
                try { _oracleTasks[x].Wait(); } catch { }
                try { _oracleTasks[x].Dispose(); } catch { }
            }
            _gcTimer.Stop();
            _cancel.Dispose();
            _cancel = null;
            _oracleTasks = null;
            // Clean queue
            _processingQueue.Clear();
            _pendingQueue.Clear();
            _accounts = new (Contract Contract, KeyPair Key)[0];
        }

        public void CleanOutOfDateOracleTask(object source, ElapsedEventArgs e)
        {
            List<UInt256> outOfDateTaskHashs = new List<UInt256>();
            foreach (var outOfDateTask in _pendingQueue)
            {
                DateTime now = TimeProvider.Current.UtcNow;
                if (now - outOfDateTask.Value.Timestamp > TimeoutInterval)
                {
                    outOfDateTaskHashs.Add(outOfDateTask.Key);
                }
                else
                    break;

            }
            foreach (UInt256 txHash in outOfDateTaskHashs)
            {
                _pendingQueue.TryRemove(txHash, out _);
            }
        }

        public void SubmitRequest(Transaction tx)
        {
            _processingQueue.Add(tx.Hash, new OracleTask(tx.Hash));
        }

        public void ProcessRequest(OracleTask task)
        {
            Log($"Process oracle request: requestTx={task.requestTxHash}");
            using var snapshot = _snapshotFactory();
            RequestState requestState = NativeContract.Oracle.GetRequestState(snapshot, task.requestTxHash);
            if (requestState is null || requestState.Status != RequestStatusType.REQUEST) return;
            OracleRequest request = requestState.Request;
            ECPoint[] oraclePublicKeys = NativeContract.Oracle.GetOracleValidators(snapshot);
            var contract = Contract.CreateMultiSigContract(oraclePublicKeys.Length - (oraclePublicKeys.Length - 1) / 3, oraclePublicKeys);

            OracleResponse response = Protocols(request);
            var responseTx = CreateResponseTransaction(snapshot, response, contract);
            if (responseTx is null) return;
            Log($"Generated response tx: requestTx={task.requestTxHash} responseTx={responseTx.Hash}");

            foreach (var account in _accounts)
            {
                var response_payload = new OraclePayload()
                {
                    OraclePub = account.Key.PublicKey,
                    OracleSignature = new OracleResponseSignature()
                    {
                        TransactionResponseHash = responseTx.Hash,
                        Signature = responseTx.Sign(account.Key),
                        TransactionRequestHash = task.requestTxHash
                    }
                };

                var signatureMsg = response_payload.Sign(account.Key);
                var signPayload = new ContractParametersContext(response_payload);

                if (signPayload.AddSignature(account.Contract, response_payload.OraclePub, signatureMsg) && signPayload.Completed)
                {
                    response_payload.Witness = signPayload.GetWitnesses()[0];
                    task.UpdateTaskState(request, response);
                    task.AddResponseItem(contract, oraclePublicKeys, new ResponseItem(response_payload, responseTx), _blockChain, _pendingQueue);
                    if (!_pendingQueue.TryAdd(task.requestTxHash, task))
                    {
                        _pendingQueue.TryGetValue(task.requestTxHash, out OracleTask new_oracleTask);
                        if (new_oracleTask != null)
                        {
                            new_oracleTask.UpdateTaskState(task.request, task.response);
                            new_oracleTask.AddResponseItem(contract, oraclePublicKeys, new ResponseItem(response_payload, responseTx), _blockChain, _pendingQueue);
                        }
                    }

                    Log($"Send oracle signature: oracle={response_payload.OraclePub} requestTx={task.requestTxHash} signaturePayload={response_payload.Hash}");
                    _blockChain.Tell(new Blockchain.RelayResult { Inventory = response_payload, Result = VerifyResult.Succeed });
                }
            }
        }

        private static Transaction CreateResponseTransaction(SnapshotView snapshot, OracleResponse response, Contract contract)
        {
            ScriptBuilder script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "invokeCallBackMethod");
            var tx = new Transaction()
            {
                Version = 0,
                ValidUntilBlock = snapshot.Height + Transaction.MaxValidUntilBlockIncrement,
                Attributes = new TransactionAttribute[]{
                    new Cosigner()
                    {
                        Account = NativeContract.Oracle.Hash,
                        AllowedContracts = new UInt160[]{ NativeContract.Oracle.Hash },
                        Scopes = WitnessScope.CalledByEntry
                    },
                    new OracleResponseAttribute()
                    {
                         Response = response,
                    }
                },
                Sender = contract.ScriptHash,
                Witnesses = new Witness[0],
                Script = script.ToArray(),
                NetworkFee = 0,
                Nonce = 0,
                SystemFee = 0
            };
            ScriptBuilder sb = new ScriptBuilder();
            sb.EmitAppCall(NativeContract.Oracle.Hash, "onPersist");
            snapshot.PersistingBlock = new Block() { Index = snapshot.Height + 1, Transactions = new Transaction[] { tx } };
            var engine = new ApplicationEngine(TriggerType.System, null, snapshot, 0, true);
            engine.LoadScript(sb.ToArray());
            if (engine.Execute() != VMState.HALT) return null;

            var state = new TransactionState
            {
                BlockIndex = snapshot.PersistingBlock.Index,
                Transaction = tx
            };
            snapshot.Transactions.Add(tx.Hash, state);
            engine = ApplicationEngine.Run(tx.Script, snapshot, tx, testMode: true);
            if (engine.State != VMState.HALT) return null;
            tx.SystemFee = engine.GasConsumed;
            int size = tx.Size;
            tx.NetworkFee += Wallet.CalculateNetworkFee(contract.Script, ref size);
            tx.NetworkFee += size * NativeContract.Policy.GetFeePerByte(snapshot);
            return tx;
        }

        public void SubmitOraclePayload(OraclePayload msg)
        {
            var snapshot = _snapshotFactory();
            RequestState requestState = NativeContract.Oracle.GetRequestState(snapshot, msg.OracleSignature.TransactionRequestHash);
            if (requestState != null && requestState.Status != RequestStatusType.REQUEST) return;
            ECPoint[] oraclePublicKeys = NativeContract.Oracle.GetOracleValidators(snapshot);
            var contract = Contract.CreateMultiSigContract(oraclePublicKeys.Length - (oraclePublicKeys.Length - 1) / 3, oraclePublicKeys);
            OracleTask task = new OracleTask(msg.OracleSignature.TransactionRequestHash, null, null);
            task.AddResponseItem(contract, oraclePublicKeys, new ResponseItem(msg), _blockChain, _pendingQueue);
            if (!_pendingQueue.TryAdd(msg.OracleSignature.TransactionRequestHash, task))
            {
                if (_pendingQueue.TryGetValue(task.requestTxHash, out OracleTask new_oracleTask))
                    new_oracleTask.AddResponseItem(contract, oraclePublicKeys, new ResponseItem(msg), _blockChain, _pendingQueue);
            }
        }

        private static void Log(string message, LogLevel level = LogLevel.Info)
        {
            Utility.Log(nameof(OracleService), level, message);
        }

        public static OracleResponse Process(OracleRequest request)
        {
            try
            {
                return request switch
                {
                    OracleHttpRequest https => HTTPSProtocol.Process(https),
                    _ => OracleResponse.CreateError(request.RequestTxHash),
                };
            }
            catch
            {
                return OracleResponse.CreateError(request.RequestTxHash);
            }
        }

        private static int SortTask(KeyValuePair<UInt256, OracleTask> a, KeyValuePair<UInt256, OracleTask> b)
        {
            return a.Value.Timestamp.CompareTo(b.Value.Timestamp);
        }
    }
}
