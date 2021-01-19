using Akka.Actor;
using Akka.Util.Internal;
using Neo.ConsoleService;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NJArray = Newtonsoft.Json.Linq.JArray;
using NJObject = Newtonsoft.Json.Linq.JObject;

namespace Neo.Plugins
{
    public class OracleService : Plugin, IPersistencePlugin
    {
        private const int RefreshInterval = 1000 * 60 * 3;

        private NEP6Wallet wallet;
        private readonly ConcurrentDictionary<ulong, OracleTask> pendingQueue = new ConcurrentDictionary<ulong, OracleTask>();
        private readonly ConcurrentDictionary<ulong, DateTime> finishedCache = new ConcurrentDictionary<ulong, DateTime>();
        private Timer timer;
        private readonly CancellationTokenSource cancelSource = new CancellationTokenSource();
        private bool started = false;
        private bool stopped = false;
        private int counter;

        private static readonly IReadOnlyDictionary<string, IOracleProtocol> protocols = new Dictionary<string, IOracleProtocol>
        {
            ["https"] = new OracleHttpsProtocol()
        };

        public override string Description => "Built-in oracle plugin";

        public OracleService()
        {
            RpcServerPlugin.RegisterMethods(this);
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
            foreach (var (_, p) in protocols)
                p.Configure();
        }

        public override void Dispose()
        {
            OnStop();
            while (!stopped)
                Thread.Sleep(100);
            foreach (var p in protocols)
                p.Value.Dispose();
        }

        [ConsoleCommand("start oracle", Category = "Oracle", Description = "Start oracle service")]
        private void OnStart()
        {
            if (started) return;
            string password = GetService<ConsoleServiceBase>().ReadUserInput("password", true);
            if (password.Length == 0)
            {
                Console.WriteLine("Cancelled");
                return;
            }

            wallet = new NEP6Wallet(Settings.Default.Wallet);
            try
            {
                wallet.Unlock(password);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                Console.WriteLine($"Failed to open wallet");
                return;
            }

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                if (!CheckOracleAvaiblable(snapshot, out ECPoint[] oracles)) throw new ArgumentException("The oracle service is unavailable");
                if (!CheckOracleAccount(wallet, oracles)) throw new ArgumentException("There is no oracle account in wallet");
            }

            started = true;

            timer = new Timer(OnTimer, null, RefreshInterval, Timeout.Infinite);

            ProcessRequestsAsync();
        }

        [ConsoleCommand("stop oracle", Category = "Oracle", Description = "Stop oracle service")]
        private void OnStop()
        {
            cancelSource.Cancel();
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
        }

        void IPersistencePlugin.OnPersist(Block block, StoreView snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (stopped || !started) return;
            if (!CheckOracleAvaiblable(snapshot, out ECPoint[] oracles) || !CheckOracleAccount(wallet, oracles))
                OnStop();
        }

        private async void OnTimer(object state)
        {
            List<ulong> outOfDate = new List<ulong>();
            foreach (var (id, task) in pendingQueue)
            {
                var span = TimeProvider.Current.UtcNow - task.Timestamp;
                if (span > TimeSpan.FromSeconds(RefreshInterval) && span < TimeSpan.FromSeconds(RefreshInterval * 2))
                {
                    List<Task> tasks = new List<Task>();
                    foreach (var account in wallet.GetAccounts())
                        if (task.BackupSigns.TryGetValue(account.GetKey().PublicKey, out byte[] sign))
                            tasks.Add(SendResponseSignatureAsync(id, sign, account.GetKey()));
                    await Task.WhenAll(tasks);
                }
                else if (span > Settings.Default.MaxTaskTimeout)
                {
                    outOfDate.Add(id);
                }
            }
            foreach (ulong requestId in outOfDate)
                pendingQueue.TryRemove(requestId, out _);
            foreach (var (key, value) in finishedCache)
                if (TimeProvider.Current.UtcNow - value > TimeSpan.FromDays(3))
                    finishedCache.TryRemove(key, out _);

            if (!cancelSource.IsCancellationRequested)
                timer?.Change(RefreshInterval, Timeout.Infinite);
        }

        [RpcMethod]
        public JObject SubmitOracleResponse(JArray _params)
        {
            if (stopped || !started) throw new InvalidOperationException();
            ECPoint oraclePub = ECPoint.DecodePoint(Convert.FromBase64String(_params[0].AsString()), ECCurve.Secp256r1);
            ulong requestId = (ulong)_params[1].AsNumber();
            byte[] txSign = Convert.FromBase64String(_params[2].AsString());
            byte[] msgSign = Convert.FromBase64String(_params[3].AsString());

            if (finishedCache.ContainsKey(requestId)) throw new RpcException(-100, "Request has already finished");

            using (SnapshotView snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var oracles = NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.Oracle, snapshot.Height + 1);
                if (!oracles.Any(p => p.Equals(oraclePub))) throw new RpcException(-100, $"{oraclePub} isn't an oracle node");
                if (NativeContract.Oracle.GetRequest(snapshot, requestId) is null)
                    throw new RpcException(-100, "Request is not found");
                var data = Neo.Helper.Concat(oraclePub.ToArray(), BitConverter.GetBytes(requestId), txSign);
                if (!Crypto.VerifySignature(data, msgSign, oraclePub)) throw new RpcException(-100, "Invalid sign");

                AddResponseTxSign(snapshot, requestId, oraclePub, txSign);
            }
            return new JObject();
        }

        private static async Task SendContentAsync(string url, string content)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 5000;
                using (StreamWriter dataStream = new StreamWriter(await request.GetRequestStreamAsync()))
                {
                    await dataStream.WriteAsync(content);
                }
                HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync();
                if (response.ContentLength > ushort.MaxValue) throw new Exception("The response it's bigger than allowed");
                StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                await reader.ReadToEndAsync();
            }
            catch (Exception e)
            {
                Log($"Failed to send the response signature to {url}, as {e.Message}", LogLevel.Warning);
            }
        }

        private async Task SendResponseSignatureAsync(ulong requestId, byte[] txSign, KeyPair keyPair)
        {
            var message = Neo.Helper.Concat(keyPair.PublicKey.ToArray(), BitConverter.GetBytes(requestId), txSign);
            var sign = Crypto.Sign(message, keyPair.PrivateKey, keyPair.PublicKey.EncodePoint(false)[1..]);
            var param = "\"" + Convert.ToBase64String(keyPair.PublicKey.ToArray()) + "\", " + requestId + ", \"" + Convert.ToBase64String(txSign) + "\",\"" + Convert.ToBase64String(sign) + "\"";
            var content = "{\"id\":" + Interlocked.Increment(ref counter) + ",\"jsonrpc\":\"2.0\",\"method\":\"submitoracleresponse\",\"params\":[" + param + "]}";

            var tasks = Settings.Default.Nodes.Select(p => SendContentAsync(p, content));
            await Task.WhenAll(tasks);
        }

        private async Task ProcessRequestAsync(StoreView snapshot, OracleRequest req)
        {
            Log($"Process oracle request: {req}, txid: {req.OriginalTxid}, url: {req.Url}");

            (OracleResponseCode code, string data) = await ProcessUrlAsync(req.Url);

            var oracleNodes = NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.Oracle, snapshot.Height + 1);
            foreach (var (requestId, request) in NativeContract.Oracle.GetRequestsByUrl(snapshot, req.Url))
            {
                var result = Array.Empty<byte>();
                if (code == OracleResponseCode.Success)
                {
                    try
                    {
                        result = Filter(data, request.Filter);
                    }
                    catch
                    {
                        code = OracleResponseCode.Error;
                    }
                }
                var response = new OracleResponse() { Id = requestId, Code = code, Result = result };
                var responseTx = CreateResponseTx(snapshot, request, response, oracleNodes);
                var backupTx = CreateResponseTx(snapshot, request, new OracleResponse() { Code = OracleResponseCode.ConsensusUnreachable, Id = requestId, Result = Array.Empty<byte>() }, oracleNodes);

                Log($"Builded response tx:{responseTx.Hash} requestTx:{request.OriginalTxid} requestId: {requestId}");

                List<Task> tasks = new List<Task>();
                ECPoint[] oraclePublicKeys = NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.Oracle, snapshot.Height + 1);
                foreach (var account in wallet.GetAccounts())
                {
                    var oraclePub = account.GetKey()?.PublicKey;
                    if (!account.HasKey || account.Lock || !oraclePublicKeys.Contains(oraclePub)) continue;

                    var txSign = responseTx.Sign(account.GetKey());
                    var backTxSign = backupTx.Sign(account.GetKey());
                    AddResponseTxSign(snapshot, requestId, oraclePub, txSign, responseTx, backupTx, backTxSign);
                    tasks.Add(SendResponseSignatureAsync(requestId, txSign, account.GetKey()));

                    Log($"Send oracle sign data: Oracle node: {oraclePub} RequestTx: {request.OriginalTxid} Sign: {txSign.ToHexString()}");
                }
                await Task.WhenAll(tasks);
            }
        }

        private async void ProcessRequestsAsync()
        {
            while (!cancelSource.IsCancellationRequested)
            {
                using (var snapshot = Blockchain.Singleton.GetSnapshot())
                {
                    foreach (var (id, request) in NativeContract.Oracle.GetRequests(snapshot))
                    {
                        if (cancelSource.IsCancellationRequested) break;
                        if (!finishedCache.ContainsKey(id) && (!pendingQueue.TryGetValue(id, out OracleTask task) || task.Tx is null))
                            await ProcessRequestAsync(snapshot, request);
                    }
                }
                if (cancelSource.IsCancellationRequested) break;
                await Task.Delay(500);
            }
            stopped = true;
        }

        private async Task<(OracleResponseCode, string)> ProcessUrlAsync(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return (OracleResponseCode.Error, null);
            if (!protocols.TryGetValue(uri.Scheme, out IOracleProtocol protocol))
                return (OracleResponseCode.ProtocolNotSupported, null);
            try
            {
                return await protocol.ProcessAsync(uri, cancelSource.Token);
            }
            catch
            {
                return (OracleResponseCode.Error, null);
            }
        }

        public static Transaction CreateResponseTx(StoreView snapshot, OracleRequest request, OracleResponse response, ECPoint[] oracleNodes)
        {
            var requestTx = snapshot.Transactions.TryGet(request.OriginalTxid);
            var n = oracleNodes.Length;
            var m = n - (n - 1) / 3;
            var oracleSignContract = Contract.CreateMultiSigContract(m, oracleNodes);

            var tx = new Transaction()
            {
                Version = 0,
                Nonce = unchecked((uint)response.Id),
                ValidUntilBlock = requestTx.BlockIndex + Transaction.MaxValidUntilBlockIncrement,
                Signers = new[]
                {
                    new Signer
                    {
                        Account = NativeContract.Oracle.Hash,
                        Scopes = WitnessScope.None
                    },
                    new Signer
                    {
                        Account = oracleSignContract.ScriptHash,
                        Scopes = WitnessScope.None
                    }
                },
                Attributes = new[] { response },
                Script = OracleResponse.FixedScript,
                Witnesses = new Witness[2]
            };
            Dictionary<UInt160, Witness> witnessDict = new Dictionary<UInt160, Witness>
            {
                [oracleSignContract.ScriptHash] = new Witness
                {
                    InvocationScript = Array.Empty<byte>(),
                    VerificationScript = oracleSignContract.Script,
                },
                [NativeContract.Oracle.Hash] = new Witness
                {
                    InvocationScript = Array.Empty<byte>(),
                    VerificationScript = Array.Empty<byte>(),
                }
            };

            UInt160[] hashes = tx.GetScriptHashesForVerifying(snapshot);
            tx.Witnesses[0] = witnessDict[hashes[0]];
            tx.Witnesses[1] = witnessDict[hashes[1]];

            // Calculate network fee

            var oracleContract = NativeContract.ContractManagement.GetContract(snapshot, NativeContract.Oracle.Hash);
            var engine = ApplicationEngine.Create(TriggerType.Verification, tx, snapshot.Clone());
            engine.LoadContract(oracleContract, "verify", CallFlags.None, true);
            engine.Push("verify");
            if (engine.Execute() != VMState.HALT) return null;
            tx.NetworkFee += engine.GasConsumed;

            var executionFactor = NativeContract.Policy.GetExecFeeFactor(snapshot);
            var networkFee = executionFactor * SmartContract.Helper.MultiSignatureContractCost(m, n);
            tx.NetworkFee += networkFee;

            // Base size for transaction: includes const_header + signers + script + hashes + witnesses, except attributes

            int size_inv = 66 * m;
            int size = Transaction.HeaderSize + tx.Signers.GetVarSize() + tx.Script.GetVarSize()
                + IO.Helper.GetVarSize(hashes.Length) + witnessDict[NativeContract.Oracle.Hash].Size
                + IO.Helper.GetVarSize(size_inv) + size_inv + oracleSignContract.Script.GetVarSize();

            var feePerByte = NativeContract.Policy.GetFeePerByte(snapshot);
            if (response.Result.Length > OracleResponse.MaxResultSize)
            {
                response.Code = OracleResponseCode.ResponseTooLarge;
                response.Result = Array.Empty<byte>();
            }
            else if (tx.NetworkFee + (size + tx.Attributes.GetVarSize()) * feePerByte > request.GasForResponse)
            {
                response.Code = OracleResponseCode.InsufficientFunds;
                response.Result = Array.Empty<byte>();
            }
            size += tx.Attributes.GetVarSize();
            tx.NetworkFee += size * feePerByte;

            // Calcualte system fee

            tx.SystemFee = request.GasForResponse - tx.NetworkFee;

            return tx;
        }

        private void AddResponseTxSign(StoreView snapshot, ulong requestId, ECPoint oraclePub, byte[] sign, Transaction responseTx = null, Transaction backupTx = null, byte[] backupSign = null)
        {
            var task = pendingQueue.GetOrAdd(requestId, _ => new OracleTask
            {
                Id = requestId,
                Request = NativeContract.Oracle.GetRequest(snapshot, requestId),
                Signs = new ConcurrentDictionary<ECPoint, byte[]>(),
                BackupSigns = new ConcurrentDictionary<ECPoint, byte[]>()
            });

            if (responseTx != null)
            {
                task.Tx = responseTx;
                var data = task.Tx.GetHashData();
                task.Signs.Where(p => !Crypto.VerifySignature(data, p.Value, p.Key)).ForEach(p => task.Signs.Remove(p.Key, out _));
            }
            if (backupTx != null)
            {
                task.BackupTx = backupTx;
                var data = task.BackupTx.GetHashData();
                task.BackupSigns.Where(p => !Crypto.VerifySignature(data, p.Value, p.Key)).ForEach(p => task.BackupSigns.Remove(p.Key, out _));
                task.BackupSigns.TryAdd(oraclePub, backupSign);
            }
            if (task.Tx == null)
            {
                task.Signs.TryAdd(oraclePub, sign);
                task.BackupSigns.TryAdd(oraclePub, sign);
                return;
            }

            if (Crypto.VerifySignature(task.Tx.GetHashData(), sign, oraclePub))
                task.Signs.TryAdd(oraclePub, sign);
            else if (Crypto.VerifySignature(task.BackupTx.GetHashData(), sign, oraclePub))
                task.BackupSigns.TryAdd(oraclePub, sign);
            else
                throw new RpcException(-100, "Invalid response transaction sign");

            if (CheckTxSign(snapshot, task.Tx, task.Signs) || CheckTxSign(snapshot, task.BackupTx, task.BackupSigns))
            {
                finishedCache.TryAdd(requestId, new DateTime());
                pendingQueue.TryRemove(requestId, out _);
            }
        }

        public static byte[] Filter(string input, string filterArgs)
        {
            if (string.IsNullOrEmpty(filterArgs))
                return Utility.StrictUTF8.GetBytes(input);

            NJObject beforeObject = NJObject.Parse(input);
            NJArray afterObjects = new NJArray(beforeObject.SelectTokens(filterArgs));
            return Utility.StrictUTF8.GetBytes(afterObjects.ToString(Newtonsoft.Json.Formatting.None));
        }

        private static bool CheckTxSign(StoreView snapshot, Transaction tx, ConcurrentDictionary<ECPoint, byte[]> OracleSigns)
        {
            ECPoint[] oraclesNodes = NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.Oracle, snapshot.Height + 1);
            int neededThreshold = oraclesNodes.Length - (oraclesNodes.Length - 1) / 3;
            if (OracleSigns.Count >= neededThreshold && tx != null)
            {
                var contract = Contract.CreateMultiSigContract(neededThreshold, oraclesNodes);
                ScriptBuilder sb = new ScriptBuilder();
                foreach (var (_, sign) in OracleSigns.OrderBy(p => p.Key))
                {
                    sb.EmitPush(sign);
                    if (--neededThreshold == 0) break;
                }
                var idx = tx.GetScriptHashesForVerifying(snapshot)[0] == contract.ScriptHash ? 0 : 1;
                tx.Witnesses[idx].InvocationScript = sb.ToArray();

                Log($"Send response tx: responseTx={tx.Hash}");

                System.Blockchain.Tell(tx);
                return true;
            }
            return false;
        }

        private static bool CheckOracleAvaiblable(StoreView snapshot, out ECPoint[] oracles)
        {
            oracles = NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.Oracle, snapshot.Height + 1);
            return oracles.Length > 0;
        }

        private static bool CheckOracleAccount(Wallet wallet, ECPoint[] oracles)
        {
            if (wallet is null) return false;
            return oracles
                .Select(p => wallet.GetAccount(p))
                .Any(p => p is not null && p.HasKey && !p.Lock);
        }

        private static void Log(string message, LogLevel level = LogLevel.Info)
        {
            Utility.Log(nameof(OracleService), level, message);
        }

        class OracleTask
        {
            public ulong Id;
            public OracleRequest Request;
            public Transaction Tx;
            public Transaction BackupTx;
            public ConcurrentDictionary<ECPoint, byte[]> Signs;
            public ConcurrentDictionary<ECPoint, byte[]> BackupSigns;
            public readonly DateTime Timestamp = TimeProvider.Current.UtcNow;
        }
    }
}
