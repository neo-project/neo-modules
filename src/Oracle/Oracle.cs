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

namespace Neo.Plugins
{
    public class Oracle : Plugin
    {
        private NEP6Wallet wallet;
        private string[] nodes;
        private readonly ConcurrentDictionary<ulong, OracleTask> pendingQueue;
        private CancellationTokenSource cancelSource;

        private static readonly IOracleProtocol Https = new OracleHttpProtocol();
        private static readonly TimeSpan MaxTaskTimeout = TimeSpan.FromMinutes(5);
        public override string Description => "Neo3 built-in oracle plugin";

        public Oracle()
        {
            pendingQueue = new ConcurrentDictionary<ulong, OracleTask>();

            //RpcServerPlugin.RegisterMethods(this);

            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Enabled = true;
            timer.Interval = 1000 * 60 * 3;
            timer.Start();
            timer.Elapsed += new ElapsedEventHandler(CleanOutOfDateTask);

            Console.WriteLine("init oracle...");
        }

        protected override void Configure()
        {   
            nodes = GetConfiguration().GetSection("Nodes").GetChildren().Select(p => p.Get<string>()).ToArray();
            wallet = new NEP6Wallet(GetConfiguration().GetSection("Wallet").ToString());
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
            wallet.Unlock(password);

            var snapshot = Blockchain.Singleton.GetSnapshot();
            var oracles = NativeContract.Oracle.GetOracleNodes(snapshot)
                .Select(u => Contract.CreateSignatureRedeemScript(u).ToScriptHash());

            var accounts = wallet?.GetAccounts()
                .Where(u => u.HasKey && !u.Lock && oracles.Contains(u.ScriptHash))
                .Select(u => (u.Contract, u.GetKey()))
                .ToArray();
            if (accounts.Length == 0) throw new ArgumentException("The wallet doesn't have any oracle accounts");

            Interlocked.Exchange(ref cancelSource, new CancellationTokenSource())?.Cancel();

            new Task(() =>
            {
                while (true)
                {
                    if (cancelSource.IsCancellationRequested)
                        return;

                    snapshot = Blockchain.Singleton.GetSnapshot();
                    var enumerator = NativeContract.Oracle.GetRequests(snapshot).GetEnumerator();
                    while (enumerator.MoveNext() && !cancelSource.IsCancellationRequested)
                        ProcessRequest(snapshot, enumerator.Current.Item1, enumerator.Current.Item2);

                    Thread.Sleep(200);
                }
            }).Start();
        }

        [ConsoleCommand("stop oracle", Category = "Oracle", Description = "Stop oracle service")]
        private void OnStop()
        {
            Interlocked.Exchange(ref cancelSource, null)?.Cancel();
        }

        public void SendResponseSignature(ulong requestId, byte[] txSign, KeyPair keyPair)
        {
            var message = keyPair.PublicKey.ToArray().Concat(BitConverter.GetBytes(requestId)).Concat(txSign).ToArray();
            var sign = Crypto.Sign(message, keyPair.PrivateKey, keyPair.PublicKey.ToArray());

            foreach (var node in nodes)
            {
                new Task(() =>
                {
                    try
                    {
                        var url = new Uri("http://" + node + "/");
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
                        response = new OracleResponse() { Id = requestId, Success = true, Result = Https.Request(requestId, request.Url, request.Filter) };
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
            foreach (var account in wallet.GetAccounts())
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
            var oracleScriptHash = Contract.CreateMultiSigContract(oracleNodes.Length - oracleNodes.Length / 3, oracleNodes).ScriptHash;

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
                        AllowedContracts = new UInt160[]{ },
                        Scopes = WitnessScope.None
                    },
                    new Signer()
                    {
                        Account = oracleScriptHash,
                        AllowedContracts = new UInt160[]{ NativeContract.Oracle.Hash },
                        Scopes = WitnessScope.CalledByEntry
                    },
                },
                Witnesses = new Witness[0],
                Script = OracleResponse.FixedScript,
                NetworkFee = 0,
                Nonce = 0,
                SystemFee = 0
            };

            var engine = ApplicationEngine.Run(tx.Script, snapshot, tx);
            if (engine.State != VMState.HALT) return null;

            tx.SystemFee = engine.GasConsumed;
            int size = tx.Size;
            tx.NetworkFee += wallet.CalculateNetworkFee(snapshot, tx);
            tx.NetworkFee += size * NativeContract.Policy.GetFeePerByte(snapshot);
            return tx;
        }

        public void AddResponseTxSign(StoreView snapshot, ulong requestId, byte[] sign, ECPoint oraclePub, Transaction respnoseTx = null)
        {
            var task = pendingQueue.GetOrAdd(requestId, new OracleTask
            {
                Id = requestId,
                Request = NativeContract.Oracle.GetRequest(snapshot, requestId),
                Signs = new Dictionary<ECPoint, byte[]>(),
            });
            task.Signs.Add(oraclePub, sign);
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
                ContractParametersContext context = new ContractParametersContext(task.Tx);
                foreach (var pair in task.Signs)
                {
                    context.AddSignature(contract, pair.Key, pair.Value); // TODO 需要验证参数的有效性
                    if (context.Completed)
                    {
                        Log($"Send response tx: responseTx={task.Tx.Hash}");

                        task.Tx.Witnesses = context.GetWitnesses();
                        pendingQueue.TryRemove(requestId, out _);
                        System.Blockchain.Tell(new Blockchain.RelayResult { Inventory = task.Tx });
                        break;
                    }
                }
            }
        }

        public void CleanOutOfDateTask(object source, ElapsedEventArgs e)
        {
            List<ulong> outofdates = new List<ulong>();
            foreach (var outOfDateTask in pendingQueue)
            {
                if (TimeProvider.Current.UtcNow - outOfDateTask.Value.Timestamp <= MaxTaskTimeout)
                    break;
                outofdates.Add(outOfDateTask.Key);
            }
            foreach (ulong requestId in outofdates)
                pendingQueue.TryRemove(requestId, out _);
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
            public Dictionary<ECPoint, byte[]> Signs;
            public readonly DateTime Timestamp = TimeProvider.Current.UtcNow;
        }
    }
}
