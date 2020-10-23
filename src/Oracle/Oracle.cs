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
using Neo.SmartContract.Native.Designate;
using Neo.SmartContract.Native.Oracle;
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

namespace Neo.Plugins
{
    public class Oracle : Plugin, IPersistencePlugin
    {
        private NEP6Wallet Wallet;
        private string[] Nodes;
        private TimeSpan MaxTaskTimeout;
        private ConcurrentDictionary<ulong, OracleTask> PendingQueue;
        private CancellationTokenSource CancelSource;
        private int Counter;

        private const int RefreshInterval = 1000 * 60 * 3;

        private static readonly OracleHttpProtocol Https = new OracleHttpProtocol();

        public override string Description => "Built-in oracle plugin";

        public Oracle()
        {
            PendingQueue = new ConcurrentDictionary<ulong, OracleTask>();

            RpcServerPlugin.RegisterMethods(this);

            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Enabled = true;
            timer.Interval = RefreshInterval;
            timer.Start();
            timer.Elapsed += new ElapsedEventHandler(OnTimer);
        }

        protected override void Configure()
        {
            var config = GetConfiguration();
            Wallet = new NEP6Wallet(Combine(PluginsDirectory, nameof(Oracle), config.GetSection("Wallet").Value));
            Nodes = config.GetSection("Nodes").GetChildren().Select(p => p.Get<string>()).ToArray();
            MaxTaskTimeout = TimeSpan.FromMilliseconds(double.Parse(config.GetSection("MaxTaskTimeout").Value));
            Https.Timeout = int.Parse(config.GetSection("HttpTimeout").Value);
            Https.AllowPrivateHost = bool.Parse(config.GetSection("AllowPrivateHost").Value);
        }

        [RpcMethod]
        public JObject SubmitOracleResponse(JArray _params)
        {
            ECPoint oraclePub = ECPoint.Parse(_params[0].AsString(), ECCurve.Secp256r1);
            ulong requestId = (ulong)_params[1].AsNumber();
            byte[] txSign = _params[2].AsString().HexToBytes();
            byte[] msgSign = _params[3].AsString().HexToBytes();

            var data = oraclePub.ToArray().Concat(BitConverter.GetBytes(requestId)).Concat(txSign).ToArray();
            if (!Crypto.VerifySignature(data, msgSign, oraclePub)) throw new RpcException(-100, "Invalid sign");

            using var snapshot = Blockchain.Singleton.GetSnapshot();
            AddResponseTxSign(snapshot, requestId, txSign, oraclePub);
            return new JObject();
        }

        [ConsoleCommand("start oracle", Category = "Oracle", Description = "Start oracle service")]
        private void OnStart(string password)
        {
            Wallet.Unlock(password);

            using var snapshot = Blockchain.Singleton.GetSnapshot();
            if (!CheckOracleAvaiblable(snapshot, out ECPoint[] oracles)) throw new ArgumentException("The oracle service is unavailable");
            if (!CheckOracleAccount(oracles)) throw new ArgumentException("There is no oracle account in wallet");

            Interlocked.Exchange(ref CancelSource, new CancellationTokenSource())?.Cancel();
            new Task(() =>
            {
                while (CancelSource?.IsCancellationRequested == false)
                {
                    using (var snapshot = Blockchain.Singleton.GetSnapshot())
                    {
                        IEnumerator<(ulong RequestId, OracleRequest Request)> enumerator = NativeContract.Oracle.GetRequests(snapshot).GetEnumerator();
                        while (enumerator.MoveNext() && !CancelSource.IsCancellationRequested)
                            if (!PendingQueue.TryGetValue(enumerator.Current.RequestId, out OracleTask task) || task.Tx is null)
                                ProcessRequest(snapshot, enumerator.Current.RequestId, enumerator.Current.Request);
                    }
                    Thread.Sleep(500);
                }
            }).Start();
        }

        void OnPersist(StoreView snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (!CheckOracleAvaiblable(snapshot, out ECPoint[] oracles) || !CheckOracleAccount(oracles))
                OnStop();
        }

        private bool CheckOracleAvaiblable(StoreView snapshot, out ECPoint[] oracles)
        {
            oracles = NativeContract.Designate.GetDesignatedByRole(snapshot, Role.Oracle);
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
        }

        public void SendResponseSignature(ulong requestId, byte[] txSign, KeyPair keyPair)
        {
            var message = keyPair.PublicKey.ToArray().Concat(BitConverter.GetBytes(requestId)).Concat(txSign).ToArray();
            var sign = Crypto.Sign(message, keyPair.PrivateKey, keyPair.PublicKey.EncodePoint(false)[1..]);
            var param = "\"" + keyPair.PublicKey.ToArray().ToHexString() + "\", " + requestId + ", \"" + txSign.ToHexString() + "\",\"" + sign.ToHexString() + "\"";
            var content = "{ \"id\": " + (++Counter) + ", \"jsonrpc\": \"2.0\",  \"method\": \"submitoracleresponse\",  \"params\":[" + param + "] }";

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

        public void ProcessRequest(StoreView snapshot, ulong requestId, OracleRequest request)
        {
            Log($"Process oracle request: {requestId}, requestTx={request.OriginalTxid}");

            Uri.TryCreate(request.Url, UriKind.Absolute, out var uri);
            OracleResponse response;
            switch (uri.Scheme.ToLowerInvariant())
            {
                case "http":
                case "https":
                    try
                    {
                        var data = Https.Request(requestId, request.Url, request.Filter);
                        response = new OracleResponse() { Id = requestId, Code = OracleResponseCode.Success, Result = data };
                    }
                    catch (Exception e)
                    {
                        Log($"Request error {e.Message}");
                        response = new OracleResponse() { Id = requestId, Code = OracleResponseCode.Error, Result = Array.Empty<byte>() };
                    }
                    break;
                default:
                    Log($"{uri.Scheme.ToLowerInvariant()} is not supported");
                    response = new OracleResponse() { Id = requestId, Code = OracleResponseCode.Forbidden, Result = Array.Empty<byte>() };
                    break;
            }

            var responseTx = CreateResponseTx(snapshot, response);
            var backupTx = CreateResponseTx(snapshot, new OracleResponse() { Code = OracleResponseCode.Error, Id = requestId, Result = Array.Empty<byte>() });

            Log($"Builded response tx:{responseTx.Hash} requestTx:{request.OriginalTxid} requestId: {requestId}");

            ECPoint[] oraclePublicKeys = NativeContract.Designate.GetDesignatedByRole(snapshot, Role.Oracle);
            foreach (var account in Wallet.GetAccounts())
            {
                var oraclePub = account.GetKey().PublicKey;
                if (!account.HasKey || account.Lock || !oraclePublicKeys.Contains(oraclePub)) continue;

                var txSign = responseTx.Sign(account.GetKey());
                var backTxSign = backupTx.Sign(account.GetKey());
                AddResponseTxSign(snapshot, requestId, txSign, oraclePub, responseTx, backupTx, backTxSign);
                SendResponseSignature(requestId, txSign, account.GetKey());

                Log($"Send oracle sign data: Oracle node: {oraclePub} RequestTx: {request.OriginalTxid} Sign: {txSign.ToHexString()} BackupSign: {backTxSign.ToHexString()}");
            }
        }

        private Transaction CreateResponseTx(StoreView snapshot, OracleResponse response)
        {
            var oracleNodes = NativeContract.Designate.GetDesignatedByRole(snapshot, Role.Oracle);
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
                VerificationScript = NativeContract.Oracle.Script,
            };

            UInt160[] hashes = tx.GetScriptHashesForVerifying(snapshot);
            tx.Witnesses[0] = witnessDict[hashes[0]];
            tx.Witnesses[1] = witnessDict[hashes[1]];

            // Calculate network fee

            var engine = ApplicationEngine.Create(TriggerType.Verification, tx, snapshot.Clone());
            engine.LoadScript(NativeContract.Oracle.Script, CallFlags.None, 0);
            engine.LoadScript(new ScriptBuilder().EmitPush(0).Emit(OpCode.PACK).EmitPush("verify").ToArray(), CallFlags.None);
            if (engine.Execute() != VMState.HALT) return null;
            tx.NetworkFee += engine.GasConsumed;

            var networkFee = ApplicationEngine.OpCodePrices[OpCode.PUSHDATA1] * m;
            using (ScriptBuilder sb = new ScriptBuilder())
                networkFee += ApplicationEngine.OpCodePrices[(OpCode)sb.EmitPush(m).ToArray()[0]];
            networkFee += ApplicationEngine.OpCodePrices[OpCode.PUSHDATA1] * n;
            using (ScriptBuilder sb = new ScriptBuilder())
                networkFee += ApplicationEngine.OpCodePrices[(OpCode)sb.EmitPush(n).ToArray()[0]];
            networkFee += ApplicationEngine.OpCodePrices[OpCode.PUSHNULL] + ApplicationEngine.ECDsaVerifyPrice * n;
            tx.NetworkFee += networkFee;

            // Base size for transaction: includes const_header + signers + script + hashes + witnesses, except attributes

            int size_inv = 66 * m;
            int size = Transaction.HeaderSize + tx.Signers.GetVarSize() + tx.Script.GetVarSize()
                + IO.Helper.GetVarSize(hashes.Length) + witnessDict[NativeContract.Oracle.Hash].Size
                + IO.Helper.GetVarSize(size_inv) + size_inv + oracleSignContract.Script.GetVarSize();

            if (tx.NetworkFee + (size + tx.Attributes.GetVarSize()) * NativeContract.Policy.GetFeePerByte(snapshot) > request.GasForResponse)
            {
                response.Code = OracleResponseCode.Error;
                response.Result = Array.Empty<byte>();
            }
            size += tx.Attributes.GetVarSize();
            tx.NetworkFee += size * NativeContract.Policy.GetFeePerByte(snapshot);

            // Calcualte system fee

            tx.SystemFee = request.GasForResponse - tx.NetworkFee;

            return tx;
        }

        public void AddResponseTxSign(StoreView snapshot, ulong requestId, byte[] sign, ECPoint oraclePub, Transaction responseTx = null, Transaction backupTx = null, byte[] backupSign = null)
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
                task.BackupTx = backupTx;
                var data = task.Tx.GetHashData();
                task.Signs.Where(p => !Crypto.VerifySignature(data, p.Value, p.Key)).ForEach(p => task.Signs.Remove(p.Key, out _));
                data = task.BackupTx.GetHashData();
                task.BackupSigns.Where(p => !Crypto.VerifySignature(data, p.Value, p.Key)).ForEach(p => task.BackupSigns.Remove(p.Key, out _));
                task.BackupSigns.TryAdd(oraclePub, backupSign);
            }

            if (task.Tx == null)
            {
                task.Signs.TryAdd(oraclePub, sign);
                task.BackupSigns.TryAdd(oraclePub, backupSign);
                return;
            }

            if (Crypto.VerifySignature(task.Tx.GetHashData(), sign, oraclePub))
                task.Signs.TryAdd(oraclePub, sign);
            else if (Crypto.VerifySignature(task.BackupTx.GetHashData(), backupSign, oraclePub))
                task.BackupSigns.TryAdd(oraclePub, backupSign);
            else
                throw new RpcException(-100, "Invalid response transaction sign");
            
            if (!CheckTxSign(snapshot, task.Tx, task.Signs))
                CheckTxSign(snapshot, task.BackupTx, task.BackupSigns);
        }

        private bool CheckTxSign(StoreView snapshot, Transaction tx, ConcurrentDictionary<ECPoint, byte[]> Signs)
        {
            ECPoint[] nodes = NativeContract.Designate.GetDesignatedByRole(snapshot, Role.Oracle);
            int m = nodes.Length - (nodes.Length - 1) / 3;
            if (Signs.Count >= m && tx != null)
            {
                var contract = Contract.CreateMultiSigContract(m, nodes);
                ScriptBuilder sb = new ScriptBuilder();
                foreach (var pair in Signs.OrderBy(p => p.Key))
                {
                    sb.EmitPush(pair.Value);
                    if (--m == 0) break;
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
                    foreach (var account in Wallet.GetAccounts())
                        if (task.Value.BackupSigns.TryGetValue(account.GetKey().PublicKey, out byte[] sign))
                            SendResponseSignature(task.Key, sign, account.GetKey());
                else if (span > MaxTaskTimeout)
                    outOfDate.Add(task.Key);
            }
                
            foreach (ulong requestId in outOfDate)
                PendingQueue.TryRemove(requestId, out _);
        }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            Utility.Log(nameof(Oracle), level, message);
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
