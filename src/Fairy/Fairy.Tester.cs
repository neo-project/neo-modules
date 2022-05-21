using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.SmartContract.Manifest;
using Neo.VM;
using System.Numerics;
using System.Collections.Concurrent;

namespace Neo.Plugins
{
    public partial class Fairy
    {
        readonly ConcurrentQueue<LogEventArgs> logs = new();

        public UInt160 neoScriptHash = UInt160.Parse("0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5");
        public UInt160 gasScriptHash = UInt160.Parse("0xd2a4cff31913016155e38e474a2c06d08be276cf");
        const byte Native_Prefix_Account = 20;
        const byte Native_Prefix_TotalSupply = 11;

        [RpcMethod]
        protected virtual JObject InvokeFunctionWithSession(JArray _params)
        {
            string session = _params[0].AsString();
            bool writeSnapshot = _params[1].AsBoolean();
            UInt160 script_hash = UInt160.Parse(_params[2].AsString());
            string operation = _params[3].AsString();
            ContractParameter[] args = _params.Count >= 5 ? ((JArray)_params[4]).Select(p => ContractParameter.FromJson(p)).ToArray() : System.Array.Empty<ContractParameter>();
            Signers signers = _params.Count >= 6 ? SignersFromJson((JArray)_params[5], system.Settings) : null;

            byte[] script;
            using (ScriptBuilder sb = new())
            {
                script = sb.EmitDynamicCall(script_hash, operation, args).ToArray();
            }
            return GetInvokeResultWithSession(session, writeSnapshot, script, signers);
        }

        [RpcMethod]
        protected virtual JObject InvokeScriptWithSession(JArray _params)
        {
            string session = _params[0].AsString();
            bool writeSnapshot = _params[1].AsBoolean();
            byte[] script = Convert.FromBase64String(_params[2].AsString());
            Signers signers = _params.Count >= 4 ? SignersFromJson((JArray)_params[3], system.Settings) : null;
            return GetInvokeResultWithSession(session, writeSnapshot, script, signers);
        }

        [RpcMethod]
        protected virtual JObject VirtualDeploy(JArray _params)
        {
            if (fairyWallet == null)
                throw new Exception("Please open a wallet before deploying a contract.");
            string session = _params[0].AsString();
            NefFile nef;
            using (var stream = new BinaryReader(new MemoryStream(Convert.FromBase64String(_params[1].AsString())), Neo.Utility.StrictUTF8, false))
            {
                nef = stream.ReadSerializable<NefFile>();
            }
            ContractManifest manifest = ContractManifest.Parse(_params[2].AsString());
            ApplicationEngine oldEngine = sessionToEngine.GetValueOrDefault(session, BuildSnapshotWithDummyScript());
            DataCache snapshot = oldEngine.Snapshot;
            byte[] script;
            using(ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(NativeContract.ContractManagement.Hash, "deploy", nef.ToArray(), manifest.ToJson().ToString());
                script = sb.ToArray();
            }
            JObject json = new();
            try
            {
                Transaction tx = fairyWallet.MakeTransaction(snapshot.CreateSnapshot(), script);
                UInt160 hash = SmartContract.Helper.GetContractHash(tx.Sender, nef.CheckSum, manifest.Name);
                sessionToEngine[session] = ApplicationEngine.Run(script, snapshot.CreateSnapshot(), persistingBlock: CreateDummyBlockWithTimestamp(oldEngine.Snapshot, system.Settings, timestamp: sessionToTimestamp.GetValueOrDefault(session, (ulong)0)), container: tx, settings: system.Settings, gas: settings.MaxGasInvoke);
                json[session] = hash.ToString();
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException.Message.StartsWith("Contract Already Exists: "))
                {
                    json[session] = ex.InnerException.Message[^42..];
                }
                else
                {
                    throw ex;
                }
            }
            return json;
        }

        [RpcMethod]
        protected virtual JObject PutStorageWithSession(JArray _params)
        {
            string session = _params[0].AsString();
            UInt160 contract = UInt160.Parse(_params[1].AsString());
            string keyBase64 = _params[2].AsString();
            byte[] key = Convert.FromBase64String(keyBase64);
            string valueBase64 = _params[3].AsString();
            byte[] value;
            if (valueBase64 == "")
            {
                value = new byte[0] { };
            }
            else
            {
                value = Convert.FromBase64String(valueBase64);
            }

            ApplicationEngine oldEngine = sessionToEngine[session];
            ContractState contractState = NativeContract.ContractManagement.GetContract(oldEngine.Snapshot, contract);
            if(value.Length == 0)
            {
                oldEngine.Snapshot.Delete(new StorageKey { Id=contractState.Id, Key=key });
            }
            else
            {
                oldEngine.Snapshot.Add(new StorageKey { Id=contractState.Id, Key=key }, new StorageItem(value));
            }
            oldEngine.Snapshot.Commit();
            JObject json = new();
            json[keyBase64] = valueBase64;
            return new JObject();
        }

        [RpcMethod]
        protected virtual JObject GetStorageWithSession(JArray _params)
        {
            string session = _params[0].AsString();
            UInt160 contract = UInt160.Parse(_params[1].AsString());
            string keyBase64 = _params[2].AsString();
            byte[] key = Convert.FromBase64String(keyBase64);

            ApplicationEngine oldEngine = sessionToEngine[session];
            ContractState contractState = NativeContract.ContractManagement.GetContract(oldEngine.Snapshot, contract);
            JObject json = new();
            StorageItem item = oldEngine.Snapshot.TryGet(new StorageKey { Id=contractState.Id, Key=key });
            json[keyBase64] = item == null ? null : Convert.ToBase64String(item.Value);
            return json;
        }

        [RpcMethod]
        protected virtual JObject SetNeoBalance(JArray _params)
        {
            string session = _params[0].AsString();
            UInt160 account = UInt160.Parse(_params[1].AsString());
            ulong balance = ulong.Parse(_params[2].AsString());
            return SetTokenBalance(session, neoScriptHash, account, balance, Native_Prefix_Account);
        }

        [RpcMethod]
        protected virtual JObject SetGasBalance(JArray _params)
        {
            string session = _params[0].AsString();
            UInt160 account = UInt160.Parse(_params[1].AsString());
            ulong balance = ulong.Parse(_params[2].AsString());
            return SetTokenBalance(session, gasScriptHash, account, balance, Native_Prefix_Account);
        }

        [RpcMethod]
        protected virtual JObject SetNep17Balance(JArray _params)
        {
            string session = _params[0].AsString();
            UInt160 contract = UInt160.Parse(_params[1].AsString());
            UInt160 account = UInt160.Parse(_params[2].AsString());
            ulong balance = ulong.Parse(_params[3].AsString());
            byte prefix = byte.Parse(_params.Count >= 5 ? _params[4].AsString() : "1");
            return SetTokenBalance(session, contract, account, balance, prefix);
        }

        private JObject SetTokenBalance(string session, UInt160 contract, UInt160 account, ulong balance, byte prefixAccount)
        {
            byte[] balanceBytes = BitConverter.GetBytes(balance);
            ApplicationEngine oldEngine = sessionToEngine[session];
            ContractState contractState = NativeContract.ContractManagement.GetContract(oldEngine.Snapshot, contract);
            JObject json = new();
            if (contract == gasScriptHash)
            {
                prefixAccount = Native_Prefix_Account;
                byte[] key = new byte[] { prefixAccount }.Concat(account.ToArray()).ToArray();
                StorageItem storage = oldEngine.Snapshot.GetAndChange(new StorageKey { Id=contractState.Id, Key=key }, () => new StorageItem(new AccountState()));
                AccountState state = storage.GetInteroperable<AccountState>();
                storage = oldEngine.Snapshot.GetAndChange(new StorageKey { Id=contractState.Id, Key=new byte[] { Native_Prefix_TotalSupply } }, () => new StorageItem(BigInteger.Zero));
                storage.Add(balance - state.Balance);
                state.Balance = balance;
                json[Convert.ToBase64String(key)] = Convert.ToBase64String(balanceBytes);
                return json;
            }else if (contract == neoScriptHash)
            {
                prefixAccount = Native_Prefix_Account;
                byte[] key = new byte[] { prefixAccount }.Concat(account.ToArray()).ToArray();
                StorageItem storage = oldEngine.Snapshot.GetAndChange(new StorageKey { Id=contractState.Id, Key=key }, () => new StorageItem(new NeoToken.NeoAccountState()));
                NeoToken.NeoAccountState state = storage.GetInteroperable<NeoToken.NeoAccountState>();
                storage = oldEngine.Snapshot.GetAndChange(new StorageKey { Id=contractState.Id, Key=new byte[] { Native_Prefix_TotalSupply } }, () => new StorageItem(BigInteger.Zero));
                storage.Add(balance - state.Balance);
                state.Balance = balance;
                json[Convert.ToBase64String(key)] = Convert.ToBase64String(balanceBytes);
                return json;
            }
            else
            {
                byte[] key = new byte[] { prefixAccount }.Concat(account.ToArray()).ToArray();
                oldEngine.Snapshot.Add(new StorageKey { Id=contractState.Id, Key=key }, new StorageItem(balanceBytes));
                json[Convert.ToBase64String(key)] = Convert.ToBase64String(balanceBytes);
                return json;
            }
        }

        private static Block CreateDummyBlockWithTimestamp(DataCache snapshot, ProtocolSettings settings, ulong timestamp=0)
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


        private ApplicationEngine BuildSnapshotWithDummyScript(ApplicationEngine engine = null)
        {
            return ApplicationEngine.Run(new byte[] { 0x40 }, engine != null ? engine.Snapshot.CreateSnapshot() : system.StoreView, settings: system.Settings, gas: settings.MaxGasInvoke);
        }

        private void CacheLog(object sender, LogEventArgs logEventArgs)
        {
            logs.Enqueue(logEventArgs);
        }

        private JObject GetInvokeResultWithSession(string session, bool writeSnapshot, byte[] script, Signers signers = null)
        {
            Transaction? tx = signers == null ? null : new Transaction
            {
                Signers = signers.GetSigners(),
                Attributes = System.Array.Empty<TransactionAttribute>(),
                Witnesses = signers.Witnesses,
            };
            ulong timestamp;
            if (!sessionToTimestamp.TryGetValue(session, out timestamp))  // we allow initializing a new session when executing
                sessionToTimestamp[session] = 0;
            ApplicationEngine oldEngine, newEngine;
            DataCache validSnapshotBase;
            Block block = null;
            logs.Clear();
            ApplicationEngine.Log += CacheLog;
            if (timestamp == 0)
            {
                if (sessionToEngine.TryGetValue(session, out oldEngine))
                {
                    newEngine = ApplicationEngine.Run(script, oldEngine.Snapshot.CreateSnapshot(), container: tx, settings: system.Settings, gas: settings.MaxGasInvoke);
                    validSnapshotBase = oldEngine.Snapshot;
                }
                else
                {
                    newEngine = ApplicationEngine.Run(script, system.StoreView, container: tx, settings: system.Settings, gas: settings.MaxGasInvoke);
                    validSnapshotBase = system.StoreView;
                }
            }
            else
            {
                oldEngine = sessionToEngine[session];
                validSnapshotBase = oldEngine.Snapshot;
                block = CreateDummyBlockWithTimestamp(oldEngine.Snapshot, system.Settings, timestamp: timestamp);
                newEngine = ApplicationEngine.Run(script, oldEngine.Snapshot.CreateSnapshot(), persistingBlock: block, container: tx, settings: system.Settings, gas: settings.MaxGasInvoke);
            }
            ApplicationEngine.Log -= CacheLog;
            if (writeSnapshot && newEngine.State == VMState.HALT)
                sessionToEngine[session] = newEngine;
            JObject json = new();
            json["script"] = Convert.ToBase64String(script);
            json["state"] = newEngine.State;
            json["gasconsumed"] = newEngine.GasConsumed.ToString();
            json["exception"] = GetExceptionMessage(newEngine.FaultException);
            if(json["exception"] != null)
            {
                string traceback = $"{json["exception"].GetString()}\r\nCallingScriptHash={newEngine.CallingScriptHash}\r\nCurrentScriptHash={newEngine.CurrentScriptHash}\r\nEntryScriptHash={newEngine.EntryScriptHash}\r\n";
                traceback += newEngine.FaultException.StackTrace;
                foreach (Neo.VM.ExecutionContext context in newEngine.InvocationStack)
                {
                    traceback += $"\r\nInstructionPointer={context.InstructionPointer}, OpCode {context.CurrentInstruction.OpCode}, Script Length={context.Script.Length}";
                }
                if(!logs.IsEmpty)
                {
                    traceback += $"\r\n-------Logs-------({logs.Count})";
                }
                foreach (LogEventArgs log in logs)
                {
                    string contractName = NativeContract.ContractManagement.GetContract(newEngine.Snapshot, log.ScriptHash).Manifest.Name;
                    traceback += $"\r\n[{log.ScriptHash}] {contractName}: {log.Message}";
                }
                json["traceback"] = traceback;
            }
            try
            {
                json["stack"] = new JArray(newEngine.ResultStack.Select(p => ToJson(p, settings.MaxIteratorResultItems)));
            }
            catch (InvalidOperationException)
            {
                json["stack"] = "error: invalid operation";
            }
            if (newEngine.State != VMState.FAULT)
            {
                ProcessInvokeWithWalletAndSnapshot(validSnapshotBase, json, signers, block: block);
            }
            return json;
        }

        private void ProcessInvokeWithWalletAndSnapshot(DataCache snapshot, JObject result, Signers signers = null, Block block = null)
        {
            if (fairyWallet == null || signers == null) return;

            Signer[] witnessSigners = signers.GetSigners().ToArray();
            UInt160? sender = signers.Size > 0 ? signers.GetSigners()[0].Account : null;
            if (witnessSigners.Length <= 0) return;

            Transaction tx;
            try
            {
                tx = fairyWallet.MakeTransaction(snapshot.CreateSnapshot(), Convert.FromBase64String(result["script"].AsString()), sender, witnessSigners, maxGas: settings.MaxGasInvoke);//, persistingBlock: block);
            }
            catch //(Exception e)
            {
                // result["exception"] = GetExceptionMessage(e);
                return;
            }
            ContractParametersContext context = new(snapshot.CreateSnapshot(), tx, system.Settings.Network);
            fairyWallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                byte[] txBytes = tx.ToArray();
                result["tx"] = Convert.ToBase64String(txBytes);
                long networkfee = (fairyWallet ?? new DummyWallet(system.Settings)).CalculateNetworkFee(system.StoreView, txBytes.AsSerializable<Transaction>());
                result["networkfee"] = networkfee.ToString();
            }
            else
            {
                result["pendingsignature"] = context.ToJson();
            }
        }
    }
}
