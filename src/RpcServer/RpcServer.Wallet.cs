#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Akka.Actor;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using Neo.Wallets.SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using static System.IO.Path;

namespace Neo.Plugins
{
    partial class RpcServer
    {
        private class DummyWallet : Wallet
        {
            public DummyWallet() : base("") { }
            public override string Name => "";
            public override Version Version => new Version();

            public override bool ChangePassword(string oldPassword, string newPassword) => false;
            public override bool Contains(UInt160 scriptHash) => false;
            public override WalletAccount CreateAccount(byte[] privateKey) => null;
            public override WalletAccount CreateAccount(Contract contract, KeyPair key = null) => null;
            public override WalletAccount CreateAccount(UInt160 scriptHash) => null;
            public override bool DeleteAccount(UInt160 scriptHash) => false;
            public override WalletAccount GetAccount(UInt160 scriptHash) => null;
            public override IEnumerable<WalletAccount> GetAccounts() => Array.Empty<WalletAccount>();
            public override bool VerifyPassword(string password) => false;
        }

        protected Wallet wallet;

        private void CheckWallet()
        {
            if (wallet is null)
                throw new RpcException(-400, "Access denied");
        }

        [RpcMethod]
        protected virtual JObject CloseWallet(JArray _params)
        {
            wallet = null;
            return true;
        }

        [RpcMethod]
        protected virtual JObject DumpPrivKey(JArray _params)
        {
            CheckWallet();
            UInt160 scriptHash = AddressToScriptHash(_params[0].AsString());
            WalletAccount account = wallet.GetAccount(scriptHash);
            return account.GetKey().Export();
        }

        [RpcMethod]
        protected virtual JObject GetNewAddress(JArray _params)
        {
            CheckWallet();
            WalletAccount account = wallet.CreateAccount();
            if (wallet is NEP6Wallet nep6)
                nep6.Save();
            return account.Address;
        }

        [RpcMethod]
        protected virtual JObject GetWalletBalance(JArray _params)
        {
            CheckWallet();
            UInt160 asset_id = UInt160.Parse(_params[0].AsString());
            JObject json = new JObject();
            json["balance"] = wallet.GetAvailable(asset_id).Value.ToString();
            return json;
        }

        [RpcMethod]
        protected virtual JObject GetWalletUnclaimedGas(JArray _params)
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
        protected virtual JObject ImportPrivKey(JArray _params)
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
        protected virtual JObject CalculateNetworkFee(JArray _params)
        {
            byte[] tx = Convert.FromBase64String(_params[0].AsString());

            JObject account = new JObject();
            account["networkfee"] = (wallet ?? new DummyWallet()).CalculateNetworkFee(Blockchain.Singleton.GetSnapshot(), tx.AsSerializable<Transaction>());
            return account;
        }

        [RpcMethod]
        protected virtual JObject ListAddress(JArray _params)
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
        protected virtual JObject OpenWallet(JArray _params)
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

        private void ProcessInvokeWithWallet(JObject result, Signers signers = null)
        {
            Transaction tx = null;
            if (wallet != null && signers != null)
            {
                Signer[] witnessSigners = signers.GetSigners().ToArray();
                UInt160 sender = signers.Size > 0 ? signers.GetSigners()[0].Account : null;
                if (witnessSigners.Count() > 0)
                {
                    tx = wallet.MakeTransaction(Convert.FromBase64String(result["script"].AsString()), sender, witnessSigners);
                    ContractParametersContext context = new ContractParametersContext(tx);
                    wallet.Sign(context);
                    if (context.Completed)
                        tx.Witnesses = context.GetWitnesses();
                    else
                        tx = null;
                }
            }
            result["tx"] = tx?.ToArray().ToHexString();
        }

        [RpcMethod]
        protected virtual JObject SendFrom(JArray _params)
        {
            CheckWallet();
            UInt160 assetId = UInt160.Parse(_params[0].AsString());
            UInt160 from = AddressToScriptHash(_params[1].AsString());
            UInt160 to = AddressToScriptHash(_params[2].AsString());
            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            BigDecimal amount = BigDecimal.Parse(_params[3].AsString(), descriptor.Decimals);
            if (amount.Sign <= 0)
                throw new RpcException(-32602, "Invalid params");
            Signer[] signers = _params.Count >= 5 ? ((JArray)_params[4]).Select(p => new Signer() { Account = AddressToScriptHash(p.AsString()), Scopes = WitnessScope.CalledByEntry }).ToArray() : null;

            Transaction tx = wallet.MakeTransaction(new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = to
                }
            }, from, signers);
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
        protected virtual JObject SendMany(JArray _params)
        {
            CheckWallet();
            int to_start = 0;
            UInt160 from = null;
            if (_params[0] is JString)
            {
                from = AddressToScriptHash(_params[0].AsString());
                to_start = 1;
            }
            JArray to = (JArray)_params[to_start];
            if (to.Count == 0)
                throw new RpcException(-32602, "Invalid params");
            Signer[] signers = _params.Count >= to_start + 2 ? ((JArray)_params[to_start + 1]).Select(p => new Signer() { Account = AddressToScriptHash(p.AsString()), Scopes = WitnessScope.CalledByEntry }).ToArray() : null;

            TransferOutput[] outputs = new TransferOutput[to.Count];
            for (int i = 0; i < to.Count; i++)
            {
                UInt160 asset_id = UInt160.Parse(to[i]["asset"].AsString());
                AssetDescriptor descriptor = new AssetDescriptor(asset_id);
                outputs[i] = new TransferOutput
                {
                    AssetId = asset_id,
                    Value = BigDecimal.Parse(to[i]["value"].AsString(), descriptor.Decimals),
                    ScriptHash = AddressToScriptHash(to[i]["address"].AsString())
                };
                if (outputs[i].Value.Sign <= 0)
                    throw new RpcException(-32602, "Invalid params");
            }
            Transaction tx = wallet.MakeTransaction(outputs, from, signers);
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
        protected virtual JObject SendToAddress(JArray _params)
        {
            CheckWallet();
            UInt160 assetId = UInt160.Parse(_params[0].AsString());
            UInt160 to = AddressToScriptHash(_params[1].AsString());
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
                    ScriptHash = to
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
                system.Blockchain.Tell(tx);
                return tx.ToJson();
            }
            else
            {
                return context.ToJson();
            }
        }

        internal static UInt160 AddressToScriptHash(string address)
        {
            if (UInt160.TryParse(address, out var scriptHash))
            {
                return scriptHash;
            }

            return address.ToScriptHash();
        }
    }
}
