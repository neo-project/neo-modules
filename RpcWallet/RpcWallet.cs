using Akka.Actor;
using Microsoft.AspNetCore.Http;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System.Linq;
using System.Numerics;

namespace Neo.Plugins
{
    public class RpcWallet : Plugin, IRpcPlugin
    {
        private Wallet Wallet => System.RpcServer.Wallet;

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            switch (method)
            {
                case "dumpprivkey":
                    {
                        UInt160 scriptHash = _params[0].AsString().ToScriptHash();
                        return DumpPrivKey(scriptHash);
                    }
                case "getbalance":
                    {
                        UInt160 asset_id = UInt160.Parse(_params[0].AsString());
                        return GetBalance(asset_id);
                    }
                case "getnewaddress":
                    {
                        return GetNewAddress();
                    }
                case "getunclaimedgas":
                    {
                        return GetUnclaimedGas();
                    }
                case "importprivkey":
                    {
                        string privkey = _params[0].AsString();
                        return ImportPrivKey(privkey);
                    }
                case "listaddress":
                    {
                        return ListAddress();
                    }
                case "sendfrom":
                    {
                        UInt160 assetId = UInt160.Parse(_params[0].AsString());
                        UInt160 from = _params[1].AsString().ToScriptHash();
                        UInt160 to = _params[2].AsString().ToScriptHash();
                        string value = _params[3].AsString();
                        return SendFrom(assetId, from, to, value);
                    }
                case "sendmany":
                    {
                        int to_start = 0;
                        UInt160 from = null;
                        if (_params[0] is JString)
                        {
                            from = _params[0].AsString().ToScriptHash();
                            to_start = 1;
                        }
                        JArray to = (JArray)_params[to_start];
                        return SendMany(from, to);
                    }
                case "sendtoaddress":
                    {
                        UInt160 assetId = UInt160.Parse(_params[0].AsString());
                        UInt160 scriptHash = _params[1].AsString().ToScriptHash();
                        string value = _params[2].AsString();
                        return SendToAddress(assetId, scriptHash, value);
                    }
                default:
                    return null;
            }
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
            switch (method)
            {
                case "invoke":
                case "invokefunction":
                case "invokescript":
                    ProcessInvoke(result);
                    break;
            }
        }

        private void ProcessInvoke(JObject result)
        {
            if (Wallet != null)
            {
                Transaction tx = Wallet.MakeTransaction(null, result["script"].AsString().HexToBytes());
                ContractParametersContext context = new ContractParametersContext(tx);
                Wallet.Sign(context);
                if (context.Completed)
                    tx.Witnesses = context.GetWitnesses();
                else
                    tx = null;
                result["tx"] = tx?.ToArray().ToHexString();
            }
        }

        private void CheckWallet()
        {
            if (Wallet is null)
                throw new RpcException(-400, "Access denied");
        }

        private JObject SignAndRelay(Transaction tx)
        {
            ContractParametersContext context = new ContractParametersContext(tx);
            Wallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                System.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                return tx.ToJson();
            }
            else
            {
                return context.ToJson();
            }
        }

        private JObject DumpPrivKey(UInt160 scriptHash)
        {
            CheckWallet();
            WalletAccount account = Wallet.GetAccount(scriptHash);
            return account.GetKey().Export();
        }

        private JObject GetBalance(UInt160 asset_id)
        {
            CheckWallet();
            JObject json = new JObject();
            json["balance"] = Wallet.GetAvailable(asset_id).ToString();
            return json;
        }

        private JObject GetNewAddress()
        {
            CheckWallet();
            WalletAccount account = Wallet.CreateAccount();
            if (Wallet is NEP6Wallet nep6)
                nep6.Save();
            return account.Address;
        }

        private JObject GetUnclaimedGas()
        {
            CheckWallet();
            BigInteger gas = BigInteger.Zero;
            using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
                foreach (UInt160 account in Wallet.GetAccounts().Select(p => p.ScriptHash))
                {
                    gas += NativeContract.NEO.UnclaimedGas(snapshot, account, snapshot.Height + 1);
                }
            return gas.ToString();
        }

        private JObject ImportPrivKey(string privkey)
        {
            CheckWallet();
            WalletAccount account = Wallet.Import(privkey);
            if (Wallet is NEP6Wallet nep6wallet)
                nep6wallet.Save();
            return new JObject
            {
                ["address"] = account.Address,
                ["haskey"] = account.HasKey,
                ["label"] = account.Label,
                ["watchonly"] = account.WatchOnly
            };
        }

        private JObject ListAddress()
        {
            CheckWallet();
            return Wallet.GetAccounts().Select(p =>
            {
                JObject account = new JObject();
                account["address"] = p.Address;
                account["haskey"] = p.HasKey;
                account["label"] = p.Label;
                account["watchonly"] = p.WatchOnly;
                return account;
            }).ToArray();
        }

        private JObject SendFrom(UInt160 assetId, UInt160 from, UInt160 to, string value)
        {
            CheckWallet();
            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            BigDecimal amount = BigDecimal.Parse(value, descriptor.Decimals);
            if (amount.Sign <= 0)
                throw new RpcException(-32602, "Invalid params");
            Transaction tx = Wallet.MakeTransaction(new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = to
                }
            }, from);
            if (tx == null)
                throw new RpcException(-300, "Insufficient funds");

            ContractParametersContext transContext = new ContractParametersContext(tx);
            Wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * 1000 + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > Settings.Default.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(tx);
        }

        private JObject SendMany(UInt160 from, JArray to)
        {
            CheckWallet();
            if (to.Count == 0)
                throw new RpcException(-32602, "Invalid params");
            TransferOutput[] outputs = new TransferOutput[to.Count];
            for (int i = 0; i < to.Count; i++)
            {
                UInt160 asset_id = UInt160.Parse(to[i]["asset"].AsString());
                AssetDescriptor descriptor = new AssetDescriptor(asset_id);
                outputs[i] = new TransferOutput
                {
                    AssetId = asset_id,
                    Value = BigDecimal.Parse(to[i]["value"].AsString(), descriptor.Decimals),
                    ScriptHash = to[i]["address"].AsString().ToScriptHash()
                };
                if (outputs[i].Value.Sign <= 0)
                    throw new RpcException(-32602, "Invalid params");
            }
            Transaction tx = Wallet.MakeTransaction(outputs, from);
            if (tx == null)
                throw new RpcException(-300, "Insufficient funds");

            ContractParametersContext transContext = new ContractParametersContext(tx);
            Wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * 1000 + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > Settings.Default.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(tx);
        }

        private JObject SendToAddress(UInt160 assetId, UInt160 scriptHash, string value)
        {
            CheckWallet();
            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            BigDecimal amount = BigDecimal.Parse(value, descriptor.Decimals);
            if (amount.Sign <= 0)
                throw new RpcException(-32602, "Invalid params");
            Transaction tx = Wallet.MakeTransaction(new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = scriptHash
                }
            });
            if (tx == null)
                throw new RpcException(-300, "Insufficient funds");

            ContractParametersContext transContext = new ContractParametersContext(tx);
            Wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * 1000 + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > Settings.Default.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(tx);
        }
    }
}
