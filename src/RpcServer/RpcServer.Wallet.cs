#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Akka.Actor;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using Neo.Wallets.SQLite;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using static System.IO.Path;

namespace Neo.Plugins
{
    partial class RpcServer
    {
        private Wallet wallet;

        private void CheckWallet()
        {
            if (wallet is null)
                throw new RpcException(-400, "Access denied");
        }

        [RpcMethod]
        private JObject CloseWallet(JArray _params)
        {
            wallet = null;
            return true;
        }

        [RpcMethod]
        private JObject DumpPrivKey(JArray _params)
        {
            CheckWallet();
            UInt160 scriptHash = _params[0].AsString().ToScriptHash();
            WalletAccount account = wallet.GetAccount(scriptHash);
            return account.GetKey().Export();
        }

        [RpcMethod]
        private JObject GetBalance(JArray _params)
        {
            CheckWallet();
            UInt160 asset_id = UInt160.Parse(_params[0].AsString());
            JObject json = new JObject();
            json["balance"] = wallet.GetAvailable(asset_id).Value.ToString();
            return json;
        }

        [RpcMethod]
        private JObject GetNewAddress(JArray _params)
        {
            CheckWallet();
            WalletAccount account = wallet.CreateAccount();
            if (wallet is NEP6Wallet nep6)
                nep6.Save();
            return account.Address;
        }

        [RpcMethod]
        private JObject GetUnclaimedGas(JArray _params)
        {
            CheckWallet();
            BigInteger gas = BigInteger.Zero;
            using (SnapshotView snapshot = Blockchain.Singleton.GetSnapshot())
                foreach (UInt160 account in wallet.GetAccounts().Select(p => p.ScriptHash))
                {
                    gas += NativeContract.NEO.UnclaimedGas(snapshot, account, snapshot.Height + 1);
                }
            return gas.ToString();
        }

        [RpcMethod]
        private JObject ImportPrivKey(JArray _params)
        {
            CheckWallet();
            string privkey = _params[0].AsString();
            WalletAccount account = wallet.Import(privkey);
            if (wallet is NEP6Wallet nep6wallet)
                nep6wallet.Save();
            return new JObject
            {
                ["address"] = account.Address,
                ["haskey"] = account.HasKey,
                ["label"] = account.Label,
                ["watchonly"] = account.WatchOnly
            };
        }

        [RpcMethod]
        private JObject ListAddress(JArray _params)
        {
            CheckWallet();
            return wallet.GetAccounts().Select(p =>
            {
                JObject account = new JObject();
                account["address"] = p.Address;
                account["haskey"] = p.HasKey;
                account["label"] = p.Label;
                account["watchonly"] = p.WatchOnly;
                return account;
            }).ToArray();
        }

        [RpcMethod]
        private JObject OpenWallet(JArray _params)
        {
            string path = _params[0].AsString();
            string password = _params[1].AsString();
            if (!File.Exists(path)) throw new FileNotFoundException();
            switch (GetExtension(path))
            {
                case ".db3":
                    {
                        wallet = UserWallet.Open(path, password);
                        break;
                    }
                case ".json":
                    {
                        NEP6Wallet nep6wallet = new NEP6Wallet(path);
                        nep6wallet.Unlock(password);
                        wallet = nep6wallet;
                        break;
                    }
                default:
                    throw new NotSupportedException();
            }
            return true;
        }

        private void ProcessInvokeWithWallet(JObject result)
        {
            if (wallet != null)
            {
                Transaction tx = wallet.MakeTransaction(result["script"].AsString().HexToBytes());
                ContractParametersContext context = new ContractParametersContext(tx);
                wallet.Sign(context);
                if (context.Completed)
                    tx.Witnesses = context.GetWitnesses();
                else
                    tx = null;
                result["tx"] = tx?.ToArray().ToHexString();
            }
        }

        [RpcMethod]
        private JObject SendFrom(JArray _params)
        {
            CheckWallet();
            UInt160 assetId = UInt160.Parse(_params[0].AsString());
            UInt160 from = _params[1].AsString().ToScriptHash();
            UInt160 to = _params[2].AsString().ToScriptHash();
            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            BigDecimal amount = BigDecimal.Parse(_params[3].AsString(), descriptor.Decimals);
            if (amount.Sign <= 0)
                throw new RpcException(-32602, "Invalid params");
            Transaction tx = wallet.MakeTransaction(new[]
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
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * 1000 + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > settings.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(tx);
        }

        [RpcMethod]
        private JObject SendMany(JArray _params)
        {
            CheckWallet();
            int to_start = 0;
            UInt160 from = null;
            if (_params[0] is JString)
            {
                from = _params[0].AsString().ToScriptHash();
                to_start = 1;
            }
            JArray to = (JArray)_params[to_start];
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
            Transaction tx = wallet.MakeTransaction(outputs, from);
            if (tx == null)
                throw new RpcException(-300, "Insufficient funds");

            ContractParametersContext transContext = new ContractParametersContext(tx);
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * 1000 + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > settings.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(tx);
        }

        [RpcMethod]
        private JObject SendToAddress(JArray _params)
        {
            CheckWallet();
            UInt160 assetId = UInt160.Parse(_params[0].AsString());
            UInt160 scriptHash = _params[1].AsString().ToScriptHash();
            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            BigDecimal amount = BigDecimal.Parse(_params[2].AsString(), descriptor.Decimals);
            if (amount.Sign <= 0)
                throw new RpcException(-32602, "Invalid params");
            Transaction tx = wallet.MakeTransaction(new[]
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
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * 1000 + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > settings.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(tx);
        }

        private JObject SignAndRelay(Transaction tx)
        {
            ContractParametersContext context = new ContractParametersContext(tx);
            wallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                system.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                return tx.ToJson();
            }
            else
            {
                return context.ToJson();
            }
        }
    }
}
