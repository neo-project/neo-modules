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
    public class Oracle : Plugin
    {
        private NEP6Wallet Wallet;
        private string[] Nodes;
        private ConcurrentDictionary<ulong, OracleTask> PendingQueue;
        private HashSet<ulong> ProcessingRequest;
        private CancellationTokenSource CancelSource;
        private TimeSpan MaxTaskTimeout;
        private TimeSpan HttpTimeout;

        private static readonly OracleHttpProtocol Https = new OracleHttpProtocol();

        public override string Description => "Built-in oracle plugin";

        public Oracle()
        {
            PendingQueue = new ConcurrentDictionary<ulong, OracleTask>();
            ProcessingRequest = new HashSet<ulong>();

            RpcServerPlugin.RegisterMethods(this);

            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Enabled = true;
            timer.Interval = 1000 * 60 * 3;
            timer.Start();
            timer.Elapsed += new ElapsedEventHandler(CleanOutOfDateTask);
        }

        protected override void Configure()
        {
            var config = GetConfiguration();
            Nodes = config.GetSection("Nodes").GetChildren().Select(p => p.Get<string>()).ToArray();
            MaxTaskTimeout = TimeSpan.FromSeconds(uint.Parse(config.GetSection("MaxTaskTimeout").Value));
            HttpTimeout = TimeSpan.FromSeconds(uint.Parse(config.GetSection("HttpTimeout").Value));
            Wallet = new NEP6Wallet(Combine(PluginsDirectory, nameof(Oracle), config.GetSection("Wallet").Value));
            Https.AllowPrivateHost = bool.Parse(config.GetSection("AllowPrivateHost").Value);
        }

        [RpcMethod]
        public void SubmitOracleReponseTxSignData(JArray _params)
        {
            var data = _params[0].ToString().HexToBytes();
            if (data.Length != 105) throw new RpcException(-100, "The length of data should be 105");

            ECPoint oraclePub = ECPoint.Parse(data.Take(33).ToArray().ToHexString(), ECCurve.Secp256r1);
            ulong requestId = BitConverter.ToUInt64(data.Skip(33).Take(8).ToArray());
            byte[] txSign = data.Skip(41).Take(32).ToArray();
            byte[] msgSign = data.Skip(73).Take(32).ToArray();

            if (Crypto.VerifySignature(data.Take(73).ToArray(), msgSign, oraclePub)) throw new RpcException(-100, "Invalid sign");

            var snapshot = Blockchain.Singleton.GetSnapshot();
            AddResponseTxSign(snapshot, requestId, txSign, oraclePub);
        }

        [ConsoleCommand("start oracle", Category = "Oracle", Description = "Start oracle service")]
        private void OnStart(string password)
        {
            Wallet.Unlock(password);

            var snapshot = Blockchain.Singleton.GetSnapshot();
            var oracles = NativeContract.Oracle.GetOracleNodes(snapshot)
                .Select(u => Contract.CreateSignatureRedeemScript(u).ToScriptHash());

            var accounts = Wallet?.GetAccounts()
                .Where(u => u.HasKey && !u.Lock && oracles.Contains(u.ScriptHash))
                .Select(u => (u.Contract, u.GetKey()))
                .ToArray();

            if (accounts.Length == 0) throw new ArgumentException("The wallet doesn't have any oracle accounts");

            Interlocked.Exchange(ref CancelSource, new CancellationTokenSource())?.Cancel();

            new Task(() =>
            {
                while (CancelSource?.IsCancellationRequested == false)
                {
                    snapshot = Blockchain.Singleton.GetSnapshot();
                    var enumerator = NativeContract.Oracle.GetRequests(snapshot).GetEnumerator();
                    while (enumerator.MoveNext() && !CancelSource.IsCancellationRequested)
                    {
                        if (ProcessingRequest.Contains(enumerator.Current.Item1)) continue;
                        ProcessingRequest.Add(enumerator.Current.Item1);

                        try
                        {
                            ProcessRequest(snapshot, enumerator.Current.Item1, enumerator.Current.Item2);
                        }
                        catch (Exception e)
                        {
                            Log(e, LogLevel.Error);
                        }
                    }

                    Thread.Sleep(2000);
                }
            }).Start();
        }

        [ConsoleCommand("stop oracle", Category = "Oracle", Description = "Stop oracle service")]
        private void OnStop()
        {
            Interlocked.Exchange(ref CancelSource, null)?.Cancel();
        }

        public void SendResponseSignature(ulong requestId, byte[] txSign, KeyPair keyPair)
        {
            var message = keyPair.PublicKey.ToArray().Concat(BitConverter.GetBytes(requestId)).Concat(txSign).ToArray();
            var sign = Crypto.Sign(message, keyPair.PrivateKey, keyPair.PublicKey.ToArray());

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
                            dataStream.Write("[{\"data\":\"" + message.Concat(sign).ToArray().ToHexString() + "\"}]");
                            dataStream.Close();
                        }
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("UTF-8"));
                        var retString = reader.ReadToEnd();
                    }
                    catch (Exception e)
                    {
                        Log($"Failed to send the response signature to {node}", LogLevel.Warning);
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
                        response = new OracleResponse() { Id = requestId, Success = true, Result = data };
                    }
                    catch (Exception e)
                    {
                        Log($"Request error {e.Message}");
                        response = new OracleResponse() { Id = requestId, Success = false, Result = Array.Empty<byte>() };
                    }
                    break;
                default:
                    Log($"{uri.Scheme.ToLowerInvariant()} is not supported");
                    response = new OracleResponse() { Id = requestId, Success = false, Result = Array.Empty<byte>() };
                    break;
            }

            var responseTx = CreateResponseTx(snapshot, response);

            Log($"Builded response tx:{responseTx.Hash} requestTx:{request.OriginalTxid} requestId: {requestId}");

            ECPoint[] oraclePublicKeys = NativeContract.Oracle.GetOracleNodes(snapshot);
            foreach (var account in Wallet.GetAccounts())
            {
                var oraclePub = account.GetKey().PublicKey;
                if (!account.HasKey || account.Lock || !oraclePublicKeys.ToList().Contains(oraclePub)) continue;

                var txSign = responseTx.Sign(account.GetKey());
                AddResponseTxSign(snapshot, requestId, txSign, oraclePub, responseTx);
                SendResponseSignature(requestId, txSign, account.GetKey());

                Log($"Send oracle sign data: Oracle node: {oraclePub} RequestTx: {request.OriginalTxid} Sign: {txSign}");
            }
        }

        private Transaction CreateResponseTx(StoreView snapshot, OracleResponse response)
        {
            var oracleNodes = NativeContract.Oracle.GetOracleNodes(snapshot);
            var m = oracleNodes.Length - (oracleNodes.Length - 1) / 3;
            var n = oracleNodes.Length;
            var oracleSignContract = Contract.CreateMultiSigContract(m, oracleNodes);

            var tx = new Transaction()
            {
                Version = 0,
                ValidUntilBlock = snapshot.Height + Transaction.MaxValidUntilBlockIncrement,
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
                InvocationScript = new ScriptBuilder().EmitPush(0).Emit(OpCode.PACK).EmitPush("verify").ToArray(),
                VerificationScript = NativeContract.Oracle.Script,
            };

            UInt160[] hashes = tx.GetScriptHashesForVerifying(snapshot);
            tx.Witnesses[0] = witnessDict[hashes[0]];
            tx.Witnesses[1] = witnessDict[hashes[1]];

            // Calcualte system fee

            var engine = ApplicationEngine.Run(tx.Script, snapshot, tx);
            if (engine.State != VMState.HALT) return null;
            tx.SystemFee = engine.GasConsumed;


            // Calculate network fee

            engine = ApplicationEngine.Create(TriggerType.Verification, tx, snapshot.Clone()); 
            engine.LoadScript(NativeContract.Oracle.Script, CallFlags.None, 0);
            engine.LoadScript(witnessDict[NativeContract.Oracle.Hash].InvocationScript, CallFlags.None);
            if (engine.Execute() != VMState.HALT) return null;
            tx.NetworkFee += engine.GasConsumed;

            // base size for transaction: includes const_header + signers + attributes + script + hashes
            int size = Transaction.HeaderSize + tx.Signers.GetVarSize() + tx.Attributes.GetVarSize() + tx.Script.GetVarSize() + IO.Helper.GetVarSize(hashes.Length);
            size += witnessDict[NativeContract.Oracle.Hash].Size;
            int size_inv = 66 * m;
            size += IO.Helper.GetVarSize(size_inv) + size_inv + oracleSignContract.Script.GetVarSize();
            var networkFee = ApplicationEngine.OpCodePrices[OpCode.PUSHDATA1] * m;
            using (ScriptBuilder sb = new ScriptBuilder())
                networkFee += ApplicationEngine.OpCodePrices[(OpCode)sb.EmitPush(m).ToArray()[0]];
            networkFee += ApplicationEngine.OpCodePrices[OpCode.PUSHDATA1] * n;
            using (ScriptBuilder sb = new ScriptBuilder())
                networkFee += ApplicationEngine.OpCodePrices[(OpCode)sb.EmitPush(n).ToArray()[0]];
            networkFee += ApplicationEngine.OpCodePrices[OpCode.PUSHNULL] + ApplicationEngine.ECDsaVerifyPrice * n;
            tx.NetworkFee += networkFee;
            tx.NetworkFee += size * NativeContract.Policy.GetFeePerByte(snapshot);
            return tx;
        }


        public void AddResponseTxSign(StoreView snapshot, ulong requestId, byte[] sign, ECPoint oraclePub, Transaction respnoseTx = null)
        {
            var task = PendingQueue.GetOrAdd(requestId, new OracleTask
            {
                Id = requestId,
                Request = NativeContract.Oracle.GetRequest(snapshot, requestId),
                Signs = new SortedDictionary<ECPoint, byte[]>(),
            });
            task.Signs.TryAdd(oraclePub, sign);
            if (respnoseTx != null)
            {
                task.Tx = respnoseTx;
                var data = task.Tx.GetHashData();
                task.Signs.Where(p => !Crypto.VerifySignature(data, p.Value, p.Key)).ForEach(p => task.Signs.Remove(p.Key));
            }
            else if (task.Tx != null)
            {
                var data = task.Tx.GetHashData();
                if (!Crypto.VerifySignature(data, sign, oraclePub))
                    throw new RpcException(-100, "Invalid response transaction sign");
            }

            ECPoint[] nodes = NativeContract.Oracle.GetOracleNodes(snapshot);
            if (task.Signs.Count >= nodes.Length / 3 && task.Tx != null)
            {
                var contract = Contract.CreateMultiSigContract(nodes.Length - (nodes.Length - 1) / 3, nodes);
                var count = nodes.Length - (nodes.Length - 1) / 3;
                ScriptBuilder sb = new ScriptBuilder();
                foreach (var pair in task.Signs)
                {
                    sb.EmitPush(pair.Value);
                    if (--count == 0) break;
                }
                var idx = 0;
                if (task.Tx.GetScriptHashesForVerifying(snapshot)[0] == NativeContract.Oracle.Hash)
                {
                    idx = 1;
                }
                task.Tx.Witnesses[idx].InvocationScript = sb.ToArray();

                Log($"Send response tx: responseTx={task.Tx.Hash}");

                PendingQueue.TryRemove(requestId, out _);
                System.Blockchain.Tell(task.Tx);
            }
        }

        public void CleanOutOfDateTask(object source, ElapsedEventArgs e)
        {
            List<ulong> outofdates = new List<ulong>();
            foreach (var outOfDateTask in PendingQueue)
            {
                if (TimeProvider.Current.UtcNow - outOfDateTask.Value.Timestamp <= MaxTaskTimeout)
                    break;
                outofdates.Add(outOfDateTask.Key);
            }
            foreach (ulong requestId in outofdates)
                PendingQueue.TryRemove(requestId, out _);

            ProcessingRequest.RemoveWhere(requestId => NativeContract.Oracle.GetRequest(Blockchain.Singleton.GetSnapshot(), requestId) is null);
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
            public SortedDictionary<ECPoint, byte[]> Signs;
            public readonly DateTime Timestamp = TimeProvider.Current.UtcNow;
        }
    }
}
