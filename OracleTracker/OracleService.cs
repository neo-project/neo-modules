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

namespace OracleTracker
{
    class OracleService
    {
        private readonly Func<SnapshotView> _snapshotFactory;
        private (Contract Contract, KeyPair Key)[] _accounts;
        private readonly IActorRef _blockChain;
        private static readonly TimeSpan TimeoutInterval = TimeSpan.FromMinutes(5);

        private long _isStarted = 0;
        private CancellationTokenSource _cancel;
        private Task[] _oracleTasks;

        public Func<OracleRequest, OracleResponse> Protocols { get; }

        private readonly SortedBlockingCollection<UInt256, Transaction> _processingQueue;

        private readonly SortedConcurrentDictionary<UInt256, RequestItem> _pendingRequests;

        private readonly SortedConcurrentDictionary<UInt256, ResponseCollection> _pendingResponses;


        public int PendingCapacity => _pendingRequests.Capacity;
        public int PendingRequestCount => _pendingRequests.Count;
        public int PendingResponseCount => _pendingResponses.Count;

        public bool IsStarted => Interlocked.Read(ref _isStarted) == 1;

        internal static IOracleProtocol HTTPSProtocol { get; } = new OracleHttpProtocol();

        private Contract _lastContract;


        public OracleService(IActorRef blockChain, int capacity)
        {
            Protocols = Process;
            _accounts = new (Contract Contract, KeyPair Key)[0];
            _snapshotFactory = new Func<SnapshotView>(() => Blockchain.Singleton.GetSnapshot());
            _blockChain = blockChain;
            _processingQueue = new SortedBlockingCollection<UInt256, Transaction>(Comparer<KeyValuePair<UInt256, Transaction>>.Create(SortDefaultProcessTx), capacity);
            _pendingRequests = new SortedConcurrentDictionary<UInt256, RequestItem>(Comparer<KeyValuePair<UInt256, RequestItem>>.Create(SortDefaultPeddingRequest), capacity);
            _pendingResponses = new SortedConcurrentDictionary<UInt256, ResponseCollection>(Comparer<KeyValuePair<UInt256, ResponseCollection>>.Create(SortDefaultPeddingResponse), capacity);
        }

        private OracleResponseResult TryAddOracleResponse(StoreView snapshot, ResponseItem response)
        {
            if (!response.Verify(snapshot))
            {
                Log($"Received wrong signed payload: oracle={response.OraclePub} requestTx={response.TransactionRequestHash} responseTx={response.TransactionRequestHash}", LogLevel.Error);

                return OracleResponseResult.Invalid;
            }

            if (!response.IsMine)
            {
                Log($"Received oracle signature: oracle={response.OraclePub} requestTx={response.TransactionRequestHash} responseTx={response.TransactionRequestHash}");
            }

            // Find the request tx

            if (_pendingRequests.TryGetValue(response.TransactionRequestHash, out var request))
            {
                // Append the signature if it's possible

                if (request.AddSignature(response))
                {
                    if (request.IsCompleted)
                    {
                        Log($"Send response tx: oracle={response.OraclePub} responseTx={request.ResponseTransaction.Hash}");

                        // Done! Send to mem pool

                        _pendingRequests.TryRemove(response.TransactionRequestHash, out _);
                        _pendingResponses.TryRemove(response.TransactionRequestHash, out _);
                        _blockChain.Tell(new Blockchain.RelayResult { Inventory = request.ResponseTransaction });
                        return OracleResponseResult.RelayedTx;
                    }

                    return OracleResponseResult.Merged;
                }
            }

            // Save this payload for check it later

            if (_pendingResponses.TryGetValue(response.TransactionRequestHash, out var collection, new ResponseCollection(response)))
            {
                if (collection != null)
                {
                    // It was getted

                    return collection.Add(response) ? OracleResponseResult.Merged : OracleResponseResult.Duplicated;
                }

                // It was added

                return OracleResponseResult.Merged;
            }

            return OracleResponseResult.Duplicated;
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

        private static Transaction CreateResponseTransaction(SnapshotView snapshot, OracleResponse response, Contract contract)
        {
            ScriptBuilder script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "invokeCallBackMethod");

            // Calculate system fee
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
                         response = response,
                    }
                },
                Sender = contract.ScriptHash,
                Witnesses = new Witness[] {
                    new Witness(){
                        InvocationScript = System.Array.Empty<byte>(),
                        VerificationScript = contract.Script
                }},
                Script = script.ToArray(),
                NetworkFee = 0,
                Nonce = 0,
                SystemFee = 0
            };

            ScriptBuilder sb = new ScriptBuilder();
            sb.EmitAppCall(NativeContract.Oracle.Hash, "onPersist");
            snapshot.PersistingBlock = new Block() { Index = snapshot.Height + 1, Transactions = new Transaction[] { tx } };
            //commit response
            var engine = new ApplicationEngine(TriggerType.System, null, snapshot, 0, true);
            engine.LoadScript(sb.ToArray());
            if (engine.Execute() != VMState.HALT) throw new InvalidOperationException();

            var state = new TransactionState
            {
                BlockIndex = snapshot.PersistingBlock.Index,
                Transaction = tx
            };
            snapshot.Transactions.Add(tx.Hash, state);

            engine = ApplicationEngine.Run(tx.Script, snapshot, tx, testMode: true);
            if (engine.State != VMState.HALT) throw new ApplicationException();
            tx.SystemFee = engine.GasConsumed;
            // Calculate network fee
            int size = tx.Size;
            tx.NetworkFee += Wallet.CalculateNetworkFee(contract.Script, ref size);
            tx.NetworkFee += size * NativeContract.Policy.GetFeePerByte(snapshot);
            return tx;
        }

        public void ProcessRequest(Transaction requestTransaction, bool forceError)
        {
            UInt256 requestTxHash = requestTransaction.Hash;
            Log($"Process oracle request: requestTx={requestTxHash} forceError={forceError}");

            using var snapshot = _snapshotFactory();
            //check request status.
            RequestState requestState = NativeContract.Oracle.GetRequestState(snapshot, requestTxHash);
            if (requestState is null) return;
            if (requestState.Status != RequestStatusType.REQUEST) return;
            OracleRequest request = requestState.Request;
            // Check the oracle contract and update the cached one
            var contract = NativeContract.Oracle.GetOracleMultiSigContract(snapshot);
            if (_lastContract?.ScriptHash != contract.ScriptHash)
            {
                // Reduce the memory load using the same Contract class
                _lastContract = contract;
            }
            else
            {
                // Use the same cached object in order to save memory in the pools
                contract = _lastContract;
            }
            OracleResponse response;
            if (!forceError)
            {
                response = Protocols(request);
            }
            else
            {
                response = OracleResponse.CreateError(requestTxHash);
            }
            // Create deterministic oracle response

            var responseTx = CreateResponseTransaction(snapshot, response, contract);

            Log($"Generated response tx: requestTx={requestTxHash} responseTx={responseTx.Hash}");

            foreach (var account in _accounts)
            {
                // Create the payload with the signed transction

                var response_payload = new OraclePayload()
                {
                    OraclePub = account.Key.PublicKey,
                    OracleSignature = new OracleResponseSignature()
                    {
                        TransactionResponseHash = responseTx.Hash,
                        Signature = responseTx.Sign(account.Key),
                        TransactionRequestHash = requestTxHash
                    }
                };

                var signatureMsg = response_payload.Sign(account.Key);
                var signPayload = new ContractParametersContext(response_payload);

                if (signPayload.AddSignature(account.Contract, response_payload.OraclePub, signatureMsg) && signPayload.Completed)
                {
                    response_payload.Witness = signPayload.GetWitnesses()[0];

                    switch (TryAddOracleResponse(snapshot, new ResponseItem(response_payload, contract, responseTx)))
                    {
                        case OracleResponseResult.Merged:
                            {
                                Log($"Send oracle signature: oracle={response_payload.OraclePub} requestTx={requestTxHash} signaturePayload={response_payload.Hash}");

                                _blockChain.Tell(new Blockchain.RelayResult { Inventory = response_payload, Result = VerifyResult.Succeed });
                                break;
                            }
                    }
                }
            }
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
                        ProcessRequest(tx, false);
                    }
                },
                _cancel.Token);
            }
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
            _cancel.Dispose();
            _cancel = null;
            _oracleTasks = null;

            // Clean queue
            _processingQueue.Clear();
            _pendingRequests.Clear();
            _pendingResponses.Clear();
            _accounts = new (Contract Contract, KeyPair Key)[0];
        }

        public void SubmitRequest(Transaction tx)
        {
            _processingQueue.Add(tx.Hash, tx);
        }

        public void SubmitOraclePayload(OraclePayload msg)
        {
            using (var snapshot = _snapshotFactory())
            {
                TryAddOracleResponse(snapshot, new ResponseItem(msg, _lastContract));
            }
        }

        private class RequestItem
        {
            public readonly Transaction RequestTransaction;
            public readonly OracleRequest request;
            public readonly ResponseCollection responseProposals;

            // Proposal

            private ResponseItem Proposal;
            private ContractParametersContext ResponseContext;

            public Transaction ResponseTransaction => Proposal?.Tx;
            public bool IsCompleted => ResponseContext?.Completed == true;

            public RequestItem(Transaction requestTx)
            {
                RequestTransaction = requestTx;
            }

            public bool AddSignature(ResponseItem response)
            {
                if (response.TransactionRequestHash != RequestTransaction.Hash)
                {
                    return false;
                }

                if (Proposal == null)
                {
                    if (!response.IsMine)
                    {
                        return false;
                    }

                    // Oracle service could attach the real TX

                    Proposal = response;
                    ResponseContext = new ContractParametersContext(response.Tx);
                }
                else
                {
                    if (response.TransactionResponseHash != Proposal.TransactionResponseHash)
                    {
                        // Unexpected result

                        return false;
                    }
                }

                if (ResponseContext.AddSignature(Proposal.Contract, response.OraclePub, response.Signature) == true)
                {
                    if (ResponseContext.Completed)
                    {
                        // Append the witness to the response TX

                        Proposal.Tx.Witnesses = ResponseContext.GetWitnesses();
                    }
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Clear responses
            /// </summary>
            public void CleanResponses()
            {
                Proposal = null;
                ResponseContext = null;
            }
        }

        private class ResponseCollection : IEnumerable<ResponseItem>
        {
            private readonly SortedConcurrentDictionary<ECPoint, ResponseItem> _items;

            public int Count => _items.Count;

            public ResponseCollection(ResponseItem item)
            {
                _items = new SortedConcurrentDictionary<ECPoint, ResponseItem>
                    (
                    Comparer<KeyValuePair<ECPoint, ResponseItem>>.Create(Sort), 1_000
                    );

                Add(item);
            }

            public bool Add(ResponseItem item)
            {
                // Prevent duplicate messages using the publicKey as key

                if (_items.TryGetValue(item.OraclePub, out var prev))
                {
                    // If it's new, replace it

                    if (prev.Timestamp > item.Timestamp) return false;

                    _items.Set(item.OraclePub, item);
                    return true;
                }

                if (_items.TryAdd(item.OraclePub, item))
                {
                    return true;
                }

                return false;
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

        private static int Sort(KeyValuePair<ECPoint, ResponseItem> a, KeyValuePair<ECPoint, ResponseItem> b)
        {
            // Sort by if it's mine or not
            int av = a.Value.IsMine ? 1 : 0;
            int bv = b.Value.IsMine ? 1 : 0;
            int ret = av.CompareTo(bv);
            if (ret != 0) return ret;
            // Sort by time
            return a.Value.Timestamp.CompareTo(b.Value.Timestamp);
        }

        private static int SortDefaultProcessTx(KeyValuePair<UInt256, Transaction> a, KeyValuePair<UInt256, Transaction> b)
        {
            // Transaction hash sorted descending
            return b.Key.CompareTo(a.Key);
        }

        private static int SortDefaultPeddingRequest(KeyValuePair<UInt256, RequestItem> a, KeyValuePair<UInt256, RequestItem> b)
        {
            // Transaction hash sorted descending
            return b.Key.CompareTo(a.Key);
        }

        private static int SortDefaultPeddingResponse(KeyValuePair<UInt256, ResponseCollection> a, KeyValuePair<UInt256, ResponseCollection> b)
        {
            // Transaction hash sorted descending
            return b.Key.CompareTo(a.Key);
        }


        private class ResponseItem
        {
            public readonly Transaction Tx;
            public readonly OracleResponseSignature Data;
            public readonly OraclePayload Msg;
            public readonly DateTime Timestamp;

            public readonly Contract Contract;
            public ECPoint OraclePub => Msg.OraclePub;
            public byte[] Signature => Data.Signature;
            public UInt256 TransactionResponseHash => Data.TransactionResponseHash;
            public UInt256 TransactionRequestHash => Data.TransactionRequestHash;
            public bool IsMine => Tx != null;

            public ResponseItem(OraclePayload payload, Contract contract = null, Transaction responseTx = null)
            {
                Tx = responseTx;
                Timestamp = TimeProvider.Current.UtcNow;
                Contract = contract;
                Msg = payload;
                Data = payload.OracleSignature;
            }

            public bool Verify(StoreView snapshot)
            {
                return Msg.Verify(snapshot);
            }
        }

        private enum OracleResponseResult : byte
        {
            Invalid,
            Merged,
            Duplicated,
            RelayedTx
        }
    }
}
