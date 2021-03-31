#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Akka.Actor;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
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
            public DummyWallet(ProtocolSettings settings) : base(null, settings) { }
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
            UInt160 scriptHash = AddressToScriptHash(_params[0].AsString(), system.Settings.AddressVersion);
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
            json["balance"] = wallet.GetAvailable(system.StoreView, asset_id).Value.ToString();
            return json;
        }

        [RpcMethod]
        protected virtual JObject GetWalletUnclaimedGas(JArray _params)
        {
            CheckWallet();
            BigInteger gas = BigInteger.Zero;
            using (var snapshot = system.GetSnapshot())
            {
                uint height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
                foreach (UInt160 account in wallet.GetAccounts().Select(p => p.ScriptHash))
                    gas += NativeContract.NEO.UnclaimedGas(snapshot, account, height);
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
            long networkfee = (wallet ?? new DummyWallet(system.Settings)).CalculateNetworkFee(system.StoreView, tx.AsSerializable<Transaction>());
            account["networkfee"] = networkfee.ToString();
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
                        wallet = UserWallet.Open(path, password, system.Settings);
                        break;
                    }
                case ".json":
                    {
                        NEP6Wallet nep6wallet = new NEP6Wallet(path, system.Settings);
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
            if (wallet == null || signers == null) return;

            Signer[] witnessSigners = signers.GetSigners().ToArray();
            UInt160 sender = signers.Size > 0 ? signers.GetSigners()[0].Account : null;
            if (witnessSigners.Length <= 0) return;

            Transaction tx;
            try
            {
                tx = wallet.MakeTransaction(system.StoreView, Convert.FromBase64String(result["script"].AsString()), sender, witnessSigners, maxGas: settings.MaxGasInvoke);
            }
            catch (Exception e)
            {
                result["exception"] = GetExceptionMessage(e);
                return;
            }
            ContractParametersContext context = new ContractParametersContext(system.StoreView, tx, settings.Network);
            wallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                result["tx"] = Convert.ToBase64String(tx.ToArray());
            }
            else
            {
                result["pendingsignature"] = context.ToJson();
            }
        }

        [RpcMethod]
        protected virtual JObject SendFrom(JArray _params)
        {
            CheckWallet();
            UInt160 assetId = UInt160.Parse(_params[0].AsString());
            UInt160 from = AddressToScriptHash(_params[1].AsString(), system.Settings.AddressVersion);
            UInt160 to = AddressToScriptHash(_params[2].AsString(), system.Settings.AddressVersion);
            using var snapshot = system.GetSnapshot();
            AssetDescriptor descriptor = new AssetDescriptor(snapshot, system.Settings, assetId);
            BigDecimal amount = new BigDecimal(BigInteger.Parse(_params[3].AsString()), descriptor.Decimals);
            if (amount.Sign <= 0)
                throw new RpcException(-32602, "Invalid params");
            Signer[] signers = _params.Count >= 5 ? ((JArray)_params[4]).Select(p => new Signer() { Account = AddressToScriptHash(p.AsString(), system.Settings.AddressVersion), Scopes = WitnessScope.CalledByEntry }).ToArray() : null;

            Transaction tx = wallet.MakeTransaction(snapshot, new[]
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

            ContractParametersContext transContext = new ContractParametersContext(snapshot, tx, settings.Network);
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
            return SignAndRelay(snapshot, tx);
        }

        [RpcMethod]
        protected virtual JObject SendMany(JArray _params)
        {
            CheckWallet();
            int to_start = 0;
            UInt160 from = null;
            if (_params[0] is JString)
            {
                from = AddressToScriptHash(_params[0].AsString(), system.Settings.AddressVersion);
                to_start = 1;
            }
            JArray to = (JArray)_params[to_start];
            if (to.Count == 0)
                throw new RpcException(-32602, "Invalid params");
            Signer[] signers = _params.Count >= to_start + 2 ? ((JArray)_params[to_start + 1]).Select(p => new Signer() { Account = AddressToScriptHash(p.AsString(), system.Settings.AddressVersion), Scopes = WitnessScope.CalledByEntry }).ToArray() : null;

            TransferOutput[] outputs = new TransferOutput[to.Count];
            using var snapshot = system.GetSnapshot();
            for (int i = 0; i < to.Count; i++)
            {
                UInt160 asset_id = UInt160.Parse(to[i]["asset"].AsString());
                AssetDescriptor descriptor = new AssetDescriptor(snapshot, system.Settings, asset_id);
                outputs[i] = new TransferOutput
                {
                    AssetId = asset_id,
                    Value = new BigDecimal(BigInteger.Parse(to[i]["value"].AsString()), descriptor.Decimals),
                    ScriptHash = AddressToScriptHash(to[i]["address"].AsString(), system.Settings.AddressVersion)
                };
                if (outputs[i].Value.Sign <= 0)
                    throw new RpcException(-32602, "Invalid params");
            }
            Transaction tx = wallet.MakeTransaction(snapshot, outputs, from, signers);
            if (tx == null)
                throw new RpcException(-300, "Insufficient funds");

            ContractParametersContext transContext = new ContractParametersContext(snapshot, tx, settings.Network);
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
            return SignAndRelay(snapshot, tx);
        }

        [RpcMethod]
        protected virtual JObject SendToAddress(JArray _params)
        {
            CheckWallet();
            UInt160 assetId = UInt160.Parse(_params[0].AsString());
            UInt160 to = AddressToScriptHash(_params[1].AsString(), system.Settings.AddressVersion);
            using var snapshot = system.GetSnapshot();
            AssetDescriptor descriptor = new AssetDescriptor(snapshot, system.Settings, assetId);
            BigDecimal amount = new BigDecimal(BigInteger.Parse(_params[2].AsString()), descriptor.Decimals);
            if (amount.Sign <= 0)
                throw new RpcException(-32602, "Invalid params");
            Transaction tx = wallet.MakeTransaction(snapshot, new[]
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

            ContractParametersContext transContext = new ContractParametersContext(snapshot, tx, settings.Network);
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
            return SignAndRelay(snapshot, tx);
        }

        [RpcMethod]
        protected virtual JObject InvokeContractVerify(JArray _params)
        {
            UInt160 script_hash = UInt160.Parse(_params[0].AsString());
            ContractParameter[] args = _params.Count >= 2 ? ((JArray)_params[1]).Select(p => ContractParameter.FromJson(p)).ToArray() : new ContractParameter[0];
            Signers signers = _params.Count >= 3 ? SignersFromJson((JArray)_params[2], system.Settings) : null;
            return GetVerificationResult(script_hash, args, signers);
        }

        private JObject GetVerificationResult(UInt160 scriptHash, ContractParameter[] args, Signers signers = null)
        {
            using var snapshot = system.GetSnapshot();
            var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);
            if (contract is null)
            {
                throw new RpcException(-100, "Unknown contract");
            }
            var md = contract.Manifest.Abi.GetMethod("verify", -1);
            if (md is null)
                throw new RpcException(-101, $"The smart contract {contract.Hash} haven't got verify method.");
            if (md.ReturnType != ContractParameterType.Boolean)
                throw new RpcException(-102, "The verify method doesn't return boolean value.");

            Transaction tx = new Transaction
            {
                Signers = signers == null ? new Signer[] { new() { Account = scriptHash } } : signers.GetSigners(),
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = signers?.Witnesses,
                Script = new[] { (byte)OpCode.RET }
            };
            using ApplicationEngine engine = ApplicationEngine.Create(TriggerType.Verification, tx, snapshot.CreateSnapshot(), settings: system.Settings);
            engine.LoadContract(contract, md, CallFlags.ReadOnly);

            var invocationScript = new byte[] { };
            if (args.Length > 0)
            {
                using ScriptBuilder sb = new ScriptBuilder();
                for (int i = args.Length - 1; i >= 0; i--)
                    sb.EmitPush(args[i]);

                invocationScript = sb.ToArray();
                tx.Witnesses ??= new Witness[] { new() { InvocationScript = invocationScript } };
                engine.LoadScript(new Script(invocationScript), configureState: p => p.CallFlags = CallFlags.None);
            }
            JObject json = new JObject();

            json["script"] = Convert.ToBase64String(invocationScript);
            json["state"] = engine.Execute();
            json["gasconsumed"] = engine.GasConsumed.ToString();
            json["exception"] = GetExceptionMessage(engine.FaultException);
            try
            {
                json["stack"] = new JArray(engine.ResultStack.Select(p => p.ToJson()));
            }
            catch (InvalidOperationException)
            {
                json["stack"] = "error: recursive reference";
            }
            return json;
        }

        private JObject SignAndRelay(DataCache snapshot, Transaction tx)
        {
            ContractParametersContext context = new ContractParametersContext(snapshot, tx, settings.Network);
            wallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                system.Blockchain.Tell(tx);
                return Utility.TransactionToJson(tx, system.Settings);
            }
            else
            {
                return context.ToJson();
            }
        }

        internal static UInt160 AddressToScriptHash(string address, byte version)
        {
            if (UInt160.TryParse(address, out var scriptHash))
            {
                return scriptHash;
            }

            return address.ToScriptHash(version);
        }
    }
}
