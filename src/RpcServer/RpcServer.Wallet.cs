// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.IO;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Neo.Plugins
{
    partial class RpcServer
    {
        private class DummyWallet : Wallet
        {
            public DummyWallet(ProtocolSettings settings) : base(null, settings) { }
            public override string Name => "";
            public override Version Version => new();

            public override bool ChangePassword(string oldPassword, string newPassword) => false;
            public override bool Contains(UInt160 scriptHash) => false;
            public override WalletAccount CreateAccount(byte[] privateKey) => null;
            public override WalletAccount CreateAccount(Contract contract, KeyPair key = null) => null;
            public override WalletAccount CreateAccount(UInt160 scriptHash) => null;
            public override void Delete() { }
            public override bool DeleteAccount(UInt160 scriptHash) => false;
            public override WalletAccount GetAccount(UInt160 scriptHash) => null;
            public override IEnumerable<WalletAccount> GetAccounts() => Array.Empty<WalletAccount>();
            public override bool VerifyPassword(string password) => false;
            public override void Save() { }
        }

        protected Wallet wallet;

        private void CheckWallet()
        {
            if (wallet is null)
                throw new RpcException(-400, "Access denied");
        }

        [RpcMethod]
        protected virtual JToken CloseWallet(JArray _params)
        {
            wallet = null;
            return true;
        }

        [RpcMethod]
        protected virtual JToken DumpPrivKey(JArray _params)
        {
            CheckWallet();
            UInt160 scriptHash = AddressToScriptHash(_params[0].AsString(), system.Settings.AddressVersion);
            WalletAccount account = wallet.GetAccount(scriptHash);
            return account.GetKey().Export();
        }

        [RpcMethod]
        protected virtual JToken GetNewAddress(JArray _params)
        {
            CheckWallet();
            WalletAccount account = wallet.CreateAccount();
            if (wallet is NEP6Wallet nep6)
                nep6.Save();
            return account.Address;
        }

        [RpcMethod]
        protected virtual JToken GetWalletBalance(JArray _params)
        {
            CheckWallet();
            UInt160 asset_id = UInt160.Parse(_params[0].AsString());
            JObject json = new();
            json["balance"] = wallet.GetAvailable(system.StoreView, asset_id).Value.ToString();
            return json;
        }

        [RpcMethod]
        protected virtual JToken GetWalletUnclaimedGas(JArray _params)
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
        protected virtual JToken ImportPrivKey(JArray _params)
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
        protected virtual JToken CalculateNetworkFee(JArray _params)
        {
            byte[] tx = Convert.FromBase64String(_params[0].AsString());

            JObject account = new();
            long networkfee = (wallet ?? new DummyWallet(system.Settings)).CalculateNetworkFee(system.StoreView, tx.AsSerializable<Transaction>());
            account["networkfee"] = networkfee.ToString();
            return account;
        }

        [RpcMethod]
        protected virtual JToken ListAddress(JArray _params)
        {
            CheckWallet();
            return wallet.GetAccounts().Select(p =>
            {
                JObject account = new();
                account["address"] = p.Address;
                account["haskey"] = p.HasKey;
                account["label"] = p.Label;
                account["watchonly"] = p.WatchOnly;
                return account;
            }).ToArray();
        }

        [RpcMethod]
        protected virtual JToken OpenWallet(JArray _params)
        {
            string path = _params[0].AsString();
            string password = _params[1].AsString();
            if (!File.Exists(path)) throw new FileNotFoundException();
            wallet = Wallet.Open(path, password, system.Settings)
                ?? throw new NotSupportedException();
            return true;
        }

        private void ProcessInvokeWithWallet(JObject result, Signer[] signers = null)
        {
            if (wallet == null || signers == null || signers.Length == 0) return;

            UInt160 sender = signers[0].Account;
            Transaction tx;
            try
            {
                tx = wallet.MakeTransaction(system.StoreView, Convert.FromBase64String(result["script"].AsString()), sender, signers, maxGas: settings.MaxGasInvoke);
            }
            catch (Exception e)
            {
                result["exception"] = GetExceptionMessage(e);
                return;
            }
            ContractParametersContext context = new(system.StoreView, tx, settings.Network);
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
        protected virtual JToken SendFrom(JArray _params)
        {
            CheckWallet();
            UInt160 assetId = UInt160.Parse(_params[0].AsString());
            UInt160 from = AddressToScriptHash(_params[1].AsString(), system.Settings.AddressVersion);
            UInt160 to = AddressToScriptHash(_params[2].AsString(), system.Settings.AddressVersion);
            using var snapshot = system.GetSnapshot();
            AssetDescriptor descriptor = new(snapshot, system.Settings, assetId);
            BigDecimal amount = new(BigInteger.Parse(_params[3].AsString()), descriptor.Decimals);
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

            ContractParametersContext transContext = new(snapshot, tx, settings.Network);
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * NativeContract.Policy.GetFeePerByte(snapshot) + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > settings.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(snapshot, tx);
        }

        [RpcMethod]
        protected virtual JToken SendMany(JArray _params)
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
                AssetDescriptor descriptor = new(snapshot, system.Settings, asset_id);
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

            ContractParametersContext transContext = new(snapshot, tx, settings.Network);
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * NativeContract.Policy.GetFeePerByte(snapshot) + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > settings.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(snapshot, tx);
        }

        [RpcMethod]
        protected virtual JToken SendToAddress(JArray _params)
        {
            CheckWallet();
            UInt160 assetId = UInt160.Parse(_params[0].AsString());
            UInt160 to = AddressToScriptHash(_params[1].AsString(), system.Settings.AddressVersion);
            using var snapshot = system.GetSnapshot();
            AssetDescriptor descriptor = new(snapshot, system.Settings, assetId);
            BigDecimal amount = new(BigInteger.Parse(_params[2].AsString()), descriptor.Decimals);
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

            ContractParametersContext transContext = new(snapshot, tx, settings.Network);
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * NativeContract.Policy.GetFeePerByte(snapshot) + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > settings.MaxFee)
                throw new RpcException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return SignAndRelay(snapshot, tx);
        }

        [RpcMethod]
        protected virtual JToken CancelTransaction(JArray _params)
        {
            CheckWallet();
            var txid = UInt256.Parse(_params[0].AsString());
            TransactionState state = NativeContract.Ledger.GetTransactionState(system.StoreView, txid);
            if (state != null)
            {
                throw new RpcException(32700, "This tx is already confirmed, can't be cancelled.");
            }

            var conflict = new TransactionAttribute[] { new Conflicts() { Hash = txid } };
            Signer[] signers = _params.Count >= 2 ? ((JArray)_params[1]).Select(j => new Signer() { Account = AddressToScriptHash(j.AsString(), system.Settings.AddressVersion), Scopes = WitnessScope.None }).ToArray() : Array.Empty<Signer>();
            if (!signers.Any())
            {
                throw new RpcException(32701, "no signers");
            }

            Transaction tx = new Transaction
            {
                Signers = signers,
                Attributes = conflict,
                Witnesses = Array.Empty<Witness>(),
            };

            try
            {
                tx = wallet.MakeTransaction(system.StoreView, new[] { (byte)OpCode.RET }, signers[0].Account, signers, conflict);
            }
            catch (InvalidOperationException e)
            {
                throw new RpcException(-500, GetExceptionMessage(e));
            }

            if (system.MemPool.TryGetValue(txid, out Transaction conflictTx))
            {
                tx.NetworkFee = Math.Max(tx.NetworkFee, conflictTx.NetworkFee) + 1;
            }
            else if (_params.Count >= 3)
            {
                var extraFee = _params[2].AsString();
                AssetDescriptor descriptor = new(system.StoreView, system.Settings, NativeContract.GAS.Hash);
                if (!BigDecimal.TryParse(extraFee, descriptor.Decimals, out BigDecimal decimalExtraFee) || decimalExtraFee.Sign <= 0)
                {
                    throw new RpcException(32702, "Incorrect Amount Format");
                }
                tx.NetworkFee += (long)decimalExtraFee.Value;
            };
            return SignAndRelay(system.StoreView, tx);
        }

        [RpcMethod]
        protected virtual JToken InvokeContractVerify(JArray _params)
        {
            UInt160 script_hash = UInt160.Parse(_params[0].AsString());
            ContractParameter[] args = _params.Count >= 2 ? ((JArray)_params[1]).Select(p => ContractParameter.FromJson((JObject)p)).ToArray() : Array.Empty<ContractParameter>();
            Signer[] signers = _params.Count >= 3 ? SignersFromJson((JArray)_params[2], system.Settings) : null;
            Witness[] witnesses = _params.Count >= 3 ? WitnessesFromJson((JArray)_params[2]) : null;
            return GetVerificationResult(script_hash, args, signers, witnesses);
        }

        private JObject GetVerificationResult(UInt160 scriptHash, ContractParameter[] args, Signer[] signers = null, Witness[] witnesses = null)
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

            Transaction tx = new()
            {
                Signers = signers ?? new Signer[] { new() { Account = scriptHash } },
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = witnesses,
                Script = new[] { (byte)OpCode.RET }
            };
            using ApplicationEngine engine = ApplicationEngine.Create(TriggerType.Verification, tx, snapshot.CreateSnapshot(), settings: system.Settings);
            engine.LoadContract(contract, md, CallFlags.ReadOnly);

            var invocationScript = Array.Empty<byte>();
            if (args.Length > 0)
            {
                using ScriptBuilder sb = new();
                for (int i = args.Length - 1; i >= 0; i--)
                    sb.EmitPush(args[i]);

                invocationScript = sb.ToArray();
                tx.Witnesses ??= new Witness[] { new() { InvocationScript = invocationScript } };
                engine.LoadScript(new Script(invocationScript), configureState: p => p.CallFlags = CallFlags.None);
            }
            JObject json = new();
            json["script"] = Convert.ToBase64String(invocationScript);
            json["state"] = engine.Execute();
            json["gasconsumed"] = engine.GasConsumed.ToString();
            json["exception"] = GetExceptionMessage(engine.FaultException);
            try
            {
                json["stack"] = new JArray(engine.ResultStack.Select(p => p.ToJson(settings.MaxStackSize)));
            }
            catch (Exception ex)
            {
                json["exception"] = ex.Message;
            }
            return json;
        }

        private JObject SignAndRelay(DataCache snapshot, Transaction tx)
        {
            ContractParametersContext context = new(snapshot, tx, settings.Network);
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
