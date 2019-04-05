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
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System.Collections.Generic;
using System.Linq;

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
                case "claimgas":
                    {
                        UInt160 to = _params.Count >= 1 ? _params[0].AsString().ToScriptHash() : null;
                        return ClaimGas(to);
                    }
                case "dumpprivkey":
                    {
                        UInt160 scriptHash = _params[0].AsString().ToScriptHash();
                        return DumpPrivKey(scriptHash);
                    }
                case "getbalance":
                    {
                        UIntBase asset_id = UIntBase.Parse(_params[0].AsString());
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
                case "getwalletheight":
                    {
                        return GetWalletHeight();
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
                        UIntBase assetId = UIntBase.Parse(_params[0].AsString());
                        UInt160 from = _params[1].AsString().ToScriptHash();
                        UInt160 to = _params[2].AsString().ToScriptHash();
                        string value = _params[3].AsString();
                        Fixed8 fee = _params.Count >= 5 ? Fixed8.Parse(_params[4].AsString()) : Fixed8.Zero;
                        UInt160 change_address = _params.Count >= 6 ? _params[5].AsString().ToScriptHash() : null;
                        return SendFrom(assetId, from, to, value, fee, change_address);
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
                        JArray to = (JArray)_params[to_start + 0];
                        Fixed8 fee = _params.Count >= to_start + 2 ? Fixed8.Parse(_params[to_start + 1].AsString()) : Fixed8.Zero;
                        UInt160 change_address = _params.Count >= to_start + 3 ? _params[to_start + 2].AsString().ToScriptHash() : null;
                        return SendMany(from, to, fee, change_address);
                    }
                case "sendtoaddress":
                    {
                        UIntBase assetId = UIntBase.Parse(_params[0].AsString());
                        UInt160 scriptHash = _params[1].AsString().ToScriptHash();
                        string value = _params[2].AsString();
                        Fixed8 fee = _params.Count >= 4 ? Fixed8.Parse(_params[3].AsString()) : Fixed8.Zero;
                        UInt160 change_address = _params.Count >= 5 ? _params[4].AsString().ToScriptHash() : null;
                        return SendToAddress(assetId, scriptHash, value, fee, change_address);
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
                InvocationTransaction tx = new InvocationTransaction
                {
                    Version = 1,
                    Script = result["script"].AsString().HexToBytes(),
                    Gas = Fixed8.Parse(result["gas_consumed"].AsString())
                };
                tx.Gas -= Fixed8.FromDecimal(10);
                if (tx.Gas < Fixed8.Zero) tx.Gas = Fixed8.Zero;
                tx.Gas = tx.Gas.Ceiling();
                tx = Wallet.MakeTransaction(tx);
                if (tx != null)
                {
                    ContractParametersContext context = new ContractParametersContext(tx);
                    Wallet.Sign(context);
                    if (context.Completed)
                        tx.Witnesses = context.GetWitnesses();
                    else
                        tx = null;
                }
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
                Wallet.ApplyTransaction(tx);
                System.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                return tx.ToJson();
            }
            else
            {
                return context.ToJson();
            }
        }

        private JObject ClaimGas(UInt160 to)
        {
            CheckWallet();
            const int MAX_CLAIMS_AMOUNT = 50;
            CoinReference[] claims = Wallet.GetUnclaimedCoins().Select(p => p.Reference).ToArray();
            if (claims.Length == 0)
                throw new RpcException(-300, "No gas to claim");
            ClaimTransaction tx;
            using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
            {
                tx = new ClaimTransaction
                {
                    Claims = claims.Take(MAX_CLAIMS_AMOUNT).ToArray(),
                    Attributes = new TransactionAttribute[0],
                    Inputs = new CoinReference[0],
                    Outputs = new[]
                    {
                        new TransactionOutput
                        {
                            AssetId = Blockchain.UtilityToken.Hash,
                            Value = snapshot.CalculateBonus(claims.Take(MAX_CLAIMS_AMOUNT)),
                            ScriptHash = to ?? Wallet.GetChangeAddress()
                        }
                    }

                };
            }
            return SignAndRelay(tx);
        }

        private JObject DumpPrivKey(UInt160 scriptHash)
        {
            CheckWallet();
            WalletAccount account = Wallet.GetAccount(scriptHash);
            return account.GetKey().Export();
        }

        private JObject GetBalance(UIntBase asset_id)
        {
            CheckWallet();
            JObject json = new JObject();
            switch (asset_id)
            {
                case UInt160 asset_id_160: //NEP-5 balance
                    json["balance"] = Wallet.GetAvailable(asset_id_160).ToString();
                    break;
                case UInt256 asset_id_256: //Global Assets balance
                    IEnumerable<Coin> coins = Wallet.GetCoins().Where(p => !p.State.HasFlag(CoinState.Spent) && p.Output.AssetId.Equals(asset_id_256));
                    json["balance"] = coins.Sum(p => p.Output.Value).ToString();
                    json["confirmed"] = coins.Where(p => p.State.HasFlag(CoinState.Confirmed)).Sum(p => p.Output.Value).ToString();
                    break;
            }
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
            using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
            {
                uint height = snapshot.Height + 1;
                Fixed8 unavailable;
                try
                {
                    unavailable = snapshot.CalculateBonus(Wallet.FindUnspentCoins().Where(p => p.Output.AssetId.Equals(Blockchain.GoverningToken.Hash)).Select(p => p.Reference), height);
                }
                catch
                {
                    unavailable = Fixed8.Zero;
                }
                return new JObject
                {
                    ["available"] = snapshot.CalculateBonus(Wallet.GetUnclaimedCoins().Select(p => p.Reference)).ToString(),
                    ["unavailable"] = unavailable.ToString()
                };
            }
        }

        private JObject GetWalletHeight()
        {
            CheckWallet();
            return (Wallet.WalletHeight > 0) ? Wallet.WalletHeight - 1 : 0;
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

        private JObject SendFrom(UIntBase assetId, UInt160 from, UInt160 to, string value, Fixed8 fee, UInt160 change_address)
        {
            CheckWallet();
            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            BigDecimal amount = BigDecimal.Parse(value, descriptor.Decimals);
            if (amount.Sign <= 0)
                throw new RpcException(-32602, "Invalid params");
            if (fee < Fixed8.Zero)
                throw new RpcException(-32602, "Invalid params");
            Transaction tx = Wallet.MakeTransaction(null, new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = to
                }
            }, from: from, change_address: change_address, fee: fee);
            if (tx == null)
                throw new RpcException(-300, "Insufficient funds");

            ContractParametersContext transContext = new ContractParametersContext(tx);
            Wallet.Sign(transContext);
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                Fixed8 calFee = Fixed8.FromDecimal(tx.Size * 0.00001m + 0.001m);
                if (fee < calFee)
                {
                    fee = calFee;
                    tx = Wallet.MakeTransaction(null, new[]
                    {
                        new TransferOutput
                            {
                                AssetId = assetId,
                                Value = amount,
                                ScriptHash = to
                            }
                    }, from: from, change_address: change_address, fee: fee);
                    if (tx == null)
                        throw new RpcException(-300, "Insufficient funds");
                }
            }
            if (fee > Settings.Default.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(tx);
        }

        private JObject SendMany(UInt160 from, JArray to, Fixed8 fee, UInt160 change_address)
        {
            CheckWallet();
            if (to.Count == 0)
                throw new RpcException(-32602, "Invalid params");
            TransferOutput[] outputs = new TransferOutput[to.Count];
            for (int i = 0; i < to.Count; i++)
            {
                UIntBase asset_id = UIntBase.Parse(to[i]["asset"].AsString());
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
            if (fee < Fixed8.Zero)
                throw new RpcException(-32602, "Invalid params");
            Transaction tx = Wallet.MakeTransaction(null, outputs, from: from, change_address: change_address, fee: fee);
            if (tx == null)
                throw new RpcException(-300, "Insufficient funds");

            ContractParametersContext transContext = new ContractParametersContext(tx);
            Wallet.Sign(transContext);
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                Fixed8 calFee = Fixed8.FromDecimal(tx.Size * 0.00001m + 0.001m);
                if (fee < calFee)
                {
                    fee = calFee;
                    tx = Wallet.MakeTransaction(null, outputs, from: from, change_address: change_address, fee: fee);
                    if (tx == null)
                        throw new RpcException(-300, "Insufficient funds");
                }   
            }
            if (fee > Settings.Default.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(tx);
        }

        private JObject SendToAddress(UIntBase assetId, UInt160 scriptHash, string value, Fixed8 fee, UInt160 change_address)
        {
            CheckWallet();
            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            BigDecimal amount = BigDecimal.Parse(value, descriptor.Decimals);
            if (amount.Sign <= 0)
                throw new RpcException(-32602, "Invalid params");
            if (fee < Fixed8.Zero)
                throw new RpcException(-32602, "Invalid params");
            Transaction tx = Wallet.MakeTransaction(null, new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = scriptHash
                }
            }, change_address: change_address, fee: fee);
            if (tx == null)
                throw new RpcException(-300, "Insufficient funds");

            ContractParametersContext transContext = new ContractParametersContext(tx);
            Wallet.Sign(transContext);
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                Fixed8 calFee = Fixed8.FromDecimal(tx.Size * 0.00001m + 0.001m);
                if (fee < calFee)
                {
                    fee = calFee;
                    tx = Wallet.MakeTransaction(null, new[]
                    {
                        new TransferOutput
                        {
                            AssetId = assetId,
                            Value = amount,
                            ScriptHash = scriptHash
                        }
                    }, change_address: change_address, fee: fee);
                    if (tx == null)
                        throw new RpcException(-300, "Insufficient funds");
                }  
            } 
            if (fee > Settings.Default.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(tx);
        }
    }
}
