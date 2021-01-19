using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Plugins.FSStorage.morph.invoke;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins.FSStorage.innerring.invoke
{
    /// <summary>
    /// MainClient is an implementation of the IClient interface.
    /// It is used to pre-execute invoking script and send script to the main chain.
    /// </summary>
    public class MainClient : IClient
    {
        public Wallet Wallet;
        public RpcClient[] Clients;

        public MainClient(string[] urls, Wallet wallet)
        {
            this.Clients = urls.Select(p => new RpcClient(p)).ToArray();
            this.Wallet = wallet;
        }

        public bool InvokeFunction(UInt160 contractHash, string method, long fee, params object[] args)
        {
            InvokeResult result = InvokeLocalFunction(contractHash, method, args);
            var blockHeight = (uint)(Clients[0].RpcSendAsync("getblockcount").Result.AsNumber());
            Random rand = new Random();
            Transaction tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)rand.Next(),
                Script = result.Script,
                ValidUntilBlock = blockHeight + Transaction.MaxValidUntilBlockIncrement - 1,
                Signers = new Signer[] { new Signer() { Account = Wallet.GetAccounts().ToArray()[0].ScriptHash, Scopes = WitnessScope.Global } },
                Attributes = Array.Empty<TransactionAttribute>(),
                SystemFee = result.GasConsumed + fee,
                NetworkFee = 0
            };
            var data = new ContractParametersContext(tx);
            Wallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var networkFee = Clients[0].RpcSendAsync("calculatenetworkfee", Convert.ToBase64String(tx.ToArray())).Result["networkfee"].AsNumber();
            tx.NetworkFee = (long)networkFee;
            data = new ContractParametersContext(tx);
            Wallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            JObject hash = Clients[0].RpcSendAsync("sendrawtransaction", Convert.ToBase64String(tx.ToArray())).Result;
            return true;
        }

        public InvokeResult InvokeLocalFunction(UInt160 contractHash, string method, params object[] args)
        {
            byte[] script = contractHash.MakeScript(method, args);
            List<JObject> parameters = new List<JObject> { Convert.ToBase64String(script) };
            Signer[] signers = new Signer[] { new Signer() { Account = Wallet.GetAccounts().ToArray()[0].ScriptHash, Scopes = WitnessScope.Global } };
            parameters.Add(signers.Select(p => p.ToJson()).ToArray());
            var result = Clients[0].RpcSendAsync("invokescript", parameters.ToArray()).Result;
            RpcInvokeResult rpcInvokeResult = RpcInvokeResult.FromJson(result);
            var r = new InvokeResult()
            {
                Script = Convert.FromBase64String(rpcInvokeResult.Script),
                State = rpcInvokeResult.State,
                GasConsumed = long.Parse(rpcInvokeResult.GasConsumed),
                ResultStack = rpcInvokeResult.Stack
            };
            return r;
        }
    }
}
