using Akka.Actor;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.ConsoleService;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using Oracle.Protocol;
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

namespace Oracle
{
    public class Oracle : Plugin
    {
        private NEP6Wallet wallet;
        private string[] nodes;
        private SnapshotView lastSnapshot;
        private readonly ConcurrentDictionary<ulong, OracleTask> pendingQueue;
        private CancellationTokenSource cancelSource;

        private static readonly IOracleProtocol Https = new OracleHttpProtocol();
        private static readonly TimeSpan TimeoutInterval = TimeSpan.FromMinutes(5);
        public override string Description => "Neo3 built-in oracle plugin";

        public Oracle()
        {
            lastSnapshot = Blockchain.Singleton.GetSnapshot();
            pendingQueue = new ConcurrentDictionary<ulong, OracleTask>();

            RpcServerPlugin.RegisterMethods(this);

            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Enabled = true;
            timer.Interval = 1000 * 60 * 3;
            timer.Start();
            timer.Elapsed += new ElapsedEventHandler(CleanOutOfDateTask);
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
            if (data.Length != 105) throw new RpcException(-100, "Data length should be ");

            ECPoint oraclePub = ECPoint.Parse(data.Take(33).ToArray().ToHexString(), ECCurve.Secp256r1);
            ulong requestId = BitConverter.ToUInt64(data.Skip(33).Take(8).ToArray());
            byte[] txSign = data.Skip(41).Take(32).ToArray();
            byte[] msgSign = data.Skip(73).Take(32).ToArray();

            if (Crypto.VerifySignature(data.Take(73).ToArray(), msgSign, oraclePub)) throw new RpcException(-100, "Invalid sign");

            AddResponseTxSign(requestId, txSign, oraclePub);
        }

        [ConsoleCommand("start oracle", Category = "Oracle", Description = "Start oracle service")]
        private void OnStart(String password)
        {
            wallet.Unlock(password);

            var oracles = NativeContract.Oracle.GetOracleNodes(lastSnapshot)
                .Select(u => Contract.CreateSignatureRedeemScript(u).ToScriptHash());

            var accounts = wallet?.GetAccounts()
                .Where(u => u.HasKey && !u.Lock && oracles.Contains(u.ScriptHash))
                .Select(u => (u.Contract, u.GetKey()))
                .ToArray();
            if (accounts.Length == 0) throw new ArgumentException("The wallet doesn't have any oracle accounts");

            cancelSource = new CancellationTokenSource();
            new Task(() =>
            {
                while (true)
                {
                    if (cancelSource.IsCancellationRequested)
                        return;

                    var enumerator = NativeContract.Oracle.GetRequests(lastSnapshot).GetEnumerator();
                    while (enumerator.MoveNext() && !cancelSource.IsCancellationRequested)
                        ProcessRequest(lastSnapshot, enumerator.Current.Item1, enumerator.Current.Item2);

                    Thread.Sleep(1000 * 1);
                }
            }).Start();
        }

        [ConsoleCommand("stop oracle", Category = "Oracle", Description = "Stop oracle service")]
        private void OnStop()
        {
            if (cancelSource != null)
            {
                cancelSource.Cancel();
                cancelSource = null;
            }
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
                        string encoding = response.ContentEncoding;
                        if (encoding == null || encoding.Length < 1)
                            encoding = "UTF-8";
                        StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding(encoding));
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
            Log($"Process oracle request: requestTx={request.OriginalTxid}");
            ECPoint[] oraclePublicKeys = NativeContract.Oracle.GetOracleNodes(snapshot);
            var contract = Contract.CreateMultiSigContract(oraclePublicKeys.Length - (oraclePublicKeys.Length - 1) / 3, oraclePublicKeys);

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

            var responseTx = CreateResponseTx(snapshot.Clone(), response, contract);
            Log($"Builded response tx:{responseTx.Hash} requestTx:{request.OriginalTxid} requestId: {requestId}");

            foreach (var account in wallet.GetAccounts())
            {
                var oraclePub = account.GetKey().PublicKey;
                if (oraclePublicKeys.ToList().Contains(oraclePub)) continue;

                var txSign = responseTx.Sign(account.GetKey());
                AddResponseTxSign(requestId, txSign, oraclePub);
                SendResponseSignature(requestId, txSign, account.GetKey());

                Log($"Send oracle sign data: Oracle node: {oraclePub} RequestTx: {request.OriginalTxid} Sign: {txSign}");
            }
        }

        private Transaction CreateResponseTx(StoreView snapshot, OracleResponse response, Contract accountContract)
        {
            var oracleNodes = NativeContract.Oracle.GetOracleNodes(snapshot);
            var oracleScriptHash = Contract.CreateMultiSigContract(oracleNodes.Length - oracleNodes.Length / 3, oracleNodes).ScriptHash;

            var tx = new Transaction()
            {
                Version = 0,
                ValidUntilBlock = snapshot.Height + Transaction.MaxValidUntilBlockIncrement,
                Attributes = new TransactionAttribute[]{
                    response
                },
                Signers = new Signer[]
                {
                    new Signer()
                    {
                        Account = accountContract.ScriptHash,
                        AllowedContracts = new UInt160[]{ NativeContract.Oracle.Hash },
                        Scopes = WitnessScope.None
                    },
                    new Signer()
                    {
                        Account = accountContract.ScriptHash,
                        AllowedContracts = new UInt160[]{ oracleScriptHash },
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

        public void AddResponseTxSign(ulong requestId, byte[] sign, ECPoint oraclePub)
        {
            var task = pendingQueue.GetOrAdd(requestId, new OracleTask
            {
                Id = requestId,
                Request = NativeContract.Oracle.GetRequest(lastSnapshot, requestId),
                Signs = new Dictionary<ECPoint, byte[]>()
            });
            task.Signs.Add(oraclePub, sign);

            ECPoint[] nodes = NativeContract.Oracle.GetOracleNodes(lastSnapshot);
            if (task.Signs.Count >= nodes.Length / 3)
            {
                var contract = Contract.CreateMultiSigContract(nodes.Length - (nodes.Length - 1) / 3, nodes);
                ContractParametersContext context = new ContractParametersContext(task.Tx);
                foreach (var pair in task.Signs)
                    context.AddSignature(contract, pair.Key, pair.Value);

                if (context.Completed)
                {
                    Log($"Send response tx: responseTx={task.Tx.Hash}");

                    task.Tx.Witnesses = context.GetWitnesses();
                    pendingQueue.TryRemove(requestId, out _);
                    System.Blockchain.Tell(new Blockchain.RelayResult { Inventory = task.Tx });
                }
            }
        }

        public void CleanOutOfDateTask(object source, ElapsedEventArgs e)
        {
            List<ulong> outOfDateTasks = new List<ulong>();
            foreach (var outOfDateTask in pendingQueue)
            {
                DateTime now = TimeProvider.Current.UtcNow;
                if (now - outOfDateTask.Value.Timestamp <= TimeoutInterval) break;
                outOfDateTasks.Add(outOfDateTask.Key);
            }
            foreach (ulong requestId in outOfDateTasks)
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
            public OracleResponse Response;
            public Transaction Tx;
            public Dictionary<ECPoint, byte[]> Signs;
            public readonly DateTime Timestamp;
        }
    }
}
