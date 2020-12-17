using Akka.Actor;
using Akka.Util.Internal;
using Microsoft.Extensions.Configuration;
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
using System.Timers;
using static System.IO.Path;
using NJArray = Newtonsoft.Json.Linq.JArray;
using NJObject = Newtonsoft.Json.Linq.JObject;
using NUtility = Neo.Utility;

namespace Neo.Plugins
{
    public class OracleService : Plugin, IPersistencePlugin
    {
        private NEP6Wallet Wallet;
        private string[] Nodes;
        private TimeSpan MaxTaskTimeout;
        private ConcurrentDictionary<ulong, OracleTask> PendingQueue;
        private CancellationTokenSource CancelSource;
        private int Counter;
        private ConcurrentDictionary<ulong, DateTime> FinishedCache;
        private ConsoleServiceBase ConsoleBase;
        private System.Timers.Timer Timer;

        private static readonly object _lock = new object();

        private const int RefreshInterval = 1000 * 60 * 3;

        private static readonly OracleHttpProtocol Https = new OracleHttpProtocol();

        public override string Description => "Built-in oracle plugin";

        public OracleService()
        {
            PendingQueue = new ConcurrentDictionary<ulong, OracleTask>();
            FinishedCache = new ConcurrentDictionary<ulong, DateTime>();

            RpcServerPlugin.RegisterMethods(this);

            ConsoleBase = GetService<ConsoleServiceBase>();
        }

        protected override void Configure()
        {
            var config = GetConfiguration();
            Wallet = new NEP6Wallet(Combine(PluginsDirectory, nameof(OracleService), config.GetSection("Wallet").Value));
            Nodes = config.GetSection("Nodes").GetChildren().Select(p => p.Get<string>()).ToArray();
            MaxTaskTimeout = TimeSpan.FromMilliseconds(double.Parse(config.GetSection("MaxTaskTimeout").Value));
            Https.Timeout = int.Parse(config.GetSection("HttpTimeout").Value);
            Https.AllowPrivateHost = bool.Parse(config.GetSection("AllowPrivateHost").Value);
        }

        [RpcMethod]
        public JObject SubmitOracleResponse(JArray _params)
        {
            ECPoint oraclePub = ECPoint.DecodePoint(Convert.FromBase64String(_params[0].AsString()), ECCurve.Secp256r1);
            ulong requestId = (ulong)_params[1].AsNumber();
            byte[] txSign = Convert.FromBase64String(_params[2].AsString());
            byte[] msgSign = Convert.FromBase64String(_params[3].AsString());

            var data = oraclePub.ToArray().Concat(BitConverter.GetBytes(requestId)).Concat(txSign).ToArray();
            if (!Crypto.VerifySignature(data, msgSign, oraclePub)) throw new RpcException(-100, "Invalid sign");
            if (FinishedCache.ContainsKey(requestId)) throw new RpcException(-100, "Request has already finished");

            using (SnapshotView snapshot = Blockchain.Singleton.GetSnapshot())
            {
                if (NativeContract.Oracle.GetRequest(snapshot, requestId) is null)
                    throw new RpcException(-100, "Request is not found");
                AddResponseTxSign(snapshot, requestId, oraclePub, txSign);
            }
            return new JObject();
        }

        [ConsoleCommand("start oracle", Category = "Oracle", Description = "Start oracle service")]
        private void OnStart()
        {
            string password = ConsoleBase.ReadUserInput("password", true);
            if (password.Length == 0)
            {
                Console.WriteLine("Cancelled");
                return;
            }
            try
            {
                Wallet.Unlock(password);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                Console.WriteLine($"Failed to open wallet");
                return;
            }

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                if (!CheckOracleAvaiblable(snapshot, out ECPoint[] oracles)) throw new ArgumentException("The oracle service is unavailable");
                if (!CheckOracleAccount(oracles)) throw new ArgumentException("There is no oracle account in wallet");
            }
            Interlocked.Exchange(ref CancelSource, new CancellationTokenSource())?.Cancel();
            new Task(() =>
            {
                while (CancelSource?.IsCancellationRequested == false)
                {
                    using (var snapshot = Blockchain.Singleton.GetSnapshot())
                    {
                        IEnumerator<(ulong RequestId, OracleRequest Request)> enumerator = NativeContract.Oracle.GetRequests(snapshot).GetEnumerator();
                        while (enumerator.MoveNext() && !CancelSource.IsCancellationRequested)
                            if (!FinishedCache.ContainsKey(enumerator.Current.RequestId) && (!PendingQueue.TryGetValue(enumerator.Current.RequestId, out OracleTask task) || task.Tx is null))
                                ProcessRequest(snapshot, enumerator.Current.RequestId, enumerator.Current.Request);
                    }
                    Thread.Sleep(500);
                }
            }).Start();

            if (Timer is null)
            {
                Timer = new System.Timers.Timer();
                Timer.Enabled = true;
                Timer.Interval = RefreshInterval;
                Timer.Start();
                Timer.Elapsed += new ElapsedEventHandler(OnTimer);
            }
        }

        void OnPersist(StoreView snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (!CheckOracleAvaiblable(snapshot, out ECPoint[] oracles) || !CheckOracleAccount(oracles))
                OnStop();
        }

        private bool CheckOracleAvaiblable(StoreView snapshot, out ECPoint[] oracles)
        {
            oracles = NativeContract.Designation.GetDesignatedByRole(snapshot, Role.Oracle, snapshot.Height + 1);
            return oracles.Length > 0;
        }

        private bool CheckOracleAccount(ECPoint[] oracles)
        {
            var oracleScriptHashes = oracles.Select(u => Contract.CreateSignatureRedeemScript(u).ToScriptHash());
            var accounts = Wallet?.GetAccounts()
                .Where(u => u.HasKey && !u.Lock && oracleScriptHashes.Contains(u.ScriptHash))
                .Select(u => (u.Contract, u.GetKey()))
                .ToArray();
            return accounts != null && accounts.Length > 0;
        }

        [ConsoleCommand("stop oracle", Category = "Oracle", Description = "Stop oracle service")]
        private void OnStop()
        {
            Interlocked.Exchange(ref CancelSource, null)?.Cancel();
            if (Timer != null)
            {
                Timer.Stop();
                Timer = null;
            }
        }

        public void SendResponseSignature(ulong requestId, byte[] txSign, KeyPair keyPair)
        {
            var message = keyPair.PublicKey.ToArray().Concat(BitConverter.GetBytes(requestId)).Concat(txSign).ToArray();
            var sign = Crypto.Sign(message, keyPair.PrivateKey, keyPair.PublicKey.EncodePoint(false)[1..]);
            var param = "\"" + Convert.ToBase64String(keyPair.PublicKey.ToArray()) + "\", " + requestId + ", \"" + Convert.ToBase64String(txSign) + "\",\"" + Convert.ToBase64String(sign) + "\"";
            var content = "{\"id\":" + (++Counter) + ",\"jsonrpc\":\"2.0\",\"method\":\"submitoracleresponse\",\"params\":[" + param + "]}";

            foreach (var node in Nodes)
            {
                new Task(() =>
                {
                    try
                    {
                        var url = new Uri(node);
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                        request.Method = "POST";
                        request.ContentType = "application/json";
                        using (StreamWriter dataStream = new StreamWriter(request.GetRequestStream()))
                        {
                            dataStream.Write(content);
                            dataStream.Close();
                        }
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        if (response.ContentLength > ushort.MaxValue) throw new ArgumentOutOfRangeException("The response it's bigger than allowed");
                        StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("UTF-8"));
                        var retString = reader.ReadToEnd();
                    }
                    catch (Exception e)
                    {
                        Log($"Failed to send the response signature to {node}, as {e.Message}", LogLevel.Warning);
                    }
                }).Start();
            }
        }

        public void ProcessRequest(StoreView snapshot, ulong id, OracleRequest req)
        {
            Log($"Process oracle request: {req}, txid: {req.OriginalTxid}, url: {req.Url}");

            string data = "";
            OracleResponseCode code = OracleResponseCode.Success;
            Uri.TryCreate(req.Url, UriKind.Absolute, out var uri);
            switch (uri.Scheme.ToLowerInvariant())
            {
                case "https":
                    try
                    {
                        data = Https.Request(req.Url);
                    }
                    catch (FileNotFoundException notfoundex)
                    {
                        Log($"Request error {notfoundex.Message}");
                        code = OracleResponseCode.NotFound;
                    }
                    catch (TimeoutException timeoutex)
                    {
                        Log($"Request error {timeoutex.Message}");
                        code = OracleResponseCode.Timeout;
                    }
                    catch (Exception e)
                    {
                        Log($"Request error {e.Message}");
                        code = OracleResponseCode.Error;
                    }
                    break;
                default:
                    Log($"{uri.Scheme.ToLowerInvariant()} is not supported");
                    code = OracleResponseCode.Forbidden;
                    break;
            }

            foreach (var (requestId, request) in NativeContract.Oracle.GetRequestsByUrl(snapshot, req.Url))
            {
                var result = code == OracleResponseCode.Success ? Filter(data, request.Filter) : Array.Empty<byte>();
                var response = new OracleResponse() { Id = requestId, Code = code, Result = result };
                var responseTx = CreateResponseTx(snapshot, response);
                var backupTx = CreateResponseTx(snapshot, new OracleResponse() { Code = OracleResponseCode.ConsensusUnreachable, Id = requestId, Result = Array.Empty<byte>() });

                Log($"Builded response tx:{responseTx.Hash} requestTx:{request.OriginalTxid} requestId: {requestId}");

                ECPoint[] oraclePublicKeys = NativeContract.Designation.GetDesignatedByRole(snapshot, Role.Oracle, snapshot.Height + 1);
                foreach (var account in Wallet.GetAccounts())
                {
                    var oraclePub = account.GetKey().PublicKey;
                    if (!account.HasKey || account.Lock || !oraclePublicKeys.Contains(oraclePub)) continue;

                    var txSign = responseTx.Sign(account.GetKey());
                    var backTxSign = backupTx.Sign(account.GetKey());
                    AddResponseTxSign(snapshot, requestId, oraclePub, txSign, responseTx, backupTx, backTxSign);
                    SendResponseSignature(requestId, txSign, account.GetKey());

                    Log($"Send oracle sign data: Oracle node: {oraclePub} RequestTx: {request.OriginalTxid} Sign: {txSign.ToHexString()}");
                }
            }
        }

        public static Transaction CreateResponseTx(StoreView snapshot, OracleResponse response)
        {
            var oracleNodes = NativeContract.Designation.GetDesignatedByRole(snapshot, Role.Oracle, snapshot.Height + 1);
            var request = NativeContract.Oracle.GetRequest(snapshot, response.Id);
            var requestTx = snapshot.Transactions.TryGet(request.OriginalTxid);
            var m = oracleNodes.Length - (oracleNodes.Length - 1) / 3;
            var n = oracleNodes.Length;
            var oracleSignContract = Contract.CreateMultiSigContract(m, oracleNodes);

            var tx = new Transaction()
            {
                Version = 0,
                ValidUntilBlock = requestTx.BlockIndex + Transaction.MaxValidUntilBlockIncrement,
                Attributes = new TransactionAttribute[] {
                    response
                },
                Signers = new Signer[]
                {
                    new Signer()
                    {
                        Account = NativeContract.Oracle.Hash,
                        AllowedContracts = new UInt160[] { },
                        Scopes = WitnessScope.None
                    },
                    new Signer()
                    {
                        Account = oracleSignContract.ScriptHash,
                        AllowedContracts = new UInt160[] { NativeContract.Oracle.Hash },
                        Scopes = WitnessScope.None
                    },
                },
                Witnesses = new Witness[2],
                Script = OracleResponse.FixedScript,
                NetworkFee = 0,
                Nonce = 0,
                SystemFee = 0
            };
            Dictionary<UInt160, Witness> witnessDict = new Dictionary<UInt160, Witness>();
            witnessDict[oracleSignContract.ScriptHash] = new Witness
            {
                InvocationScript = Array.Empty<byte>(),
                VerificationScript = oracleSignContract.Script,
            };
            witnessDict[NativeContract.Oracle.Hash] = new Witness
            {
                InvocationScript = Array.Empty<byte>(),
                VerificationScript = Array.Empty<byte>(),
            };

            UInt160[] hashes = tx.GetScriptHashesForVerifying(snapshot);
            tx.Witnesses[0] = witnessDict[hashes[0]];
            tx.Witnesses[1] = witnessDict[hashes[1]];

            // Calculate network fee

            var oracleContract = NativeContract.Management.GetContract(snapshot, NativeContract.Oracle.Hash);
            var engine = ApplicationEngine.Create(TriggerType.Verification, tx, snapshot.Clone());
            engine.LoadContract(oracleContract, "verify", CallFlags.None, true);
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

            if (response.Result.Length > OracleResponse.MaxResultSize)
            {
                response.Code = OracleResponseCode.ResponseTooLarge;
                response.Result = Array.Empty<byte>();
            }
            else if (tx.NetworkFee + (size + tx.Attributes.GetVarSize()) * NativeContract.Policy.GetFeePerByte(snapshot) > request.GasForResponse)
            {
                response.Code = OracleResponseCode.InsufficientFunds;
                response.Result = Array.Empty<byte>();
            }
            size += tx.Attributes.GetVarSize();
            tx.NetworkFee += size * NativeContract.Policy.GetFeePerByte(snapshot);

            // Calcualte system fee

            tx.SystemFee = request.GasForResponse - tx.NetworkFee;

            return tx;
        }

        public void AddResponseTxSign(StoreView snapshot, ulong requestId, ECPoint oraclePub, byte[] sign, Transaction responseTx = null, Transaction backupTx = null, byte[] backupSign = null)
        {
            lock (_lock)
            {
                var task = PendingQueue.GetOrAdd(requestId, new OracleTask
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
                    FinishedCache.TryAdd(requestId, new DateTime());
                    PendingQueue.Remove(requestId, out _);
                }
            }
        }

        private byte[] Filter(string input, string filterArgs)
        {
            if (string.IsNullOrEmpty(filterArgs))
                return NUtility.StrictUTF8.GetBytes(input);

            NJObject beforeObject = NJObject.Parse(input);
            NJArray afterObjects = new NJArray(beforeObject.SelectTokens(filterArgs, true));
            return NUtility.StrictUTF8.GetBytes(afterObjects.ToString());
        }

        private bool CheckTxSign(StoreView snapshot, Transaction tx, ConcurrentDictionary<ECPoint, byte[]> OracleSigns)
        {
            ECPoint[] oraclesNodes = NativeContract.Designation.GetDesignatedByRole(snapshot, Role.Oracle, snapshot.Height + 1);
            int neededThreshold = oraclesNodes.Length - (oraclesNodes.Length - 1) / 3;
            if (OracleSigns.Count >= neededThreshold && tx != null)
            {
                var contract = Contract.CreateMultiSigContract(neededThreshold, oraclesNodes);
                ScriptBuilder sb = new ScriptBuilder();
                foreach (var pair in OracleSigns.OrderBy(p => p.Key))
                {
                    sb.EmitPush(pair.Value);
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

        public void OnTimer(object source, ElapsedEventArgs e)
        {
            List<ulong> outOfDate = new List<ulong>();
            foreach (var task in PendingQueue)
            {
                var span = TimeProvider.Current.UtcNow - task.Value.Timestamp;
                if (span > TimeSpan.FromSeconds(RefreshInterval) && span < TimeSpan.FromSeconds(RefreshInterval * 2))
                {
                    foreach (var account in Wallet.GetAccounts())
                        if (task.Value.BackupSigns.TryGetValue(account.GetKey().PublicKey, out byte[] sign))
                            SendResponseSignature(task.Key, sign, account.GetKey());
                }
                else if (span > MaxTaskTimeout)
                {
                    outOfDate.Add(task.Key);
                }
            }
            foreach (ulong requestId in outOfDate)
                PendingQueue.TryRemove(requestId, out _);
            foreach (var key in FinishedCache.Keys)
                if (TimeProvider.Current.UtcNow - FinishedCache[key] > TimeSpan.FromDays(3))
                    FinishedCache.TryRemove(key, out _);
        }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            NUtility.Log(nameof(OracleService), level, message);
        }

        internal class OracleTask
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
