#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System;
using System.IO;
using System.Linq;

namespace Neo.Plugins
{
    partial class RpcServer
    {
        private class Signers : IVerifiable
        {
            private readonly Signer[] _signers;
            public Witness[] Witnesses { get; set; }
            public int Size => _signers.Length;

            public Signers(Signer[] signers)
            {
                _signers = signers;
            }

            public void Serialize(BinaryWriter writer)
            {
                throw new NotImplementedException();
            }

            public void Deserialize(BinaryReader reader)
            {
                throw new NotImplementedException();
            }

            public void DeserializeUnsigned(BinaryReader reader)
            {
                throw new NotImplementedException();
            }

            public UInt160[] GetScriptHashesForVerifying(DataCache snapshot)
            {
                return _signers.Select(p => p.Account).ToArray();
            }

            public Signer[] GetSigners()
            {
                return _signers;
            }

            public void SerializeUnsigned(BinaryWriter writer)
            {
                throw new NotImplementedException();
            }
        }

        private JObject GetInvokeResult(byte[] script, Signers signers = null)
        {
            Transaction tx = signers == null ? null : new Transaction
            {
                Signers = signers.GetSigners(),
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = signers.Witnesses,
            };
            using ApplicationEngine engine = ApplicationEngine.Run(script, system.StoreView, container: tx, settings: system.Settings, gas: settings.MaxGasInvoke);
            JObject json = new JObject();
            json["script"] = Convert.ToBase64String(script);
            json["state"] = engine.State;
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
            if (engine.State != VMState.FAULT)
            {
                ProcessInvokeWithWallet(json, signers);
            }
            return json;
        }

        private static Signers SignersFromJson(JArray _params, ProtocolSettings settings)
        {
            var ret = new Signers(_params.Select(u => new Signer()
            {
                Account = AddressToScriptHash(u["account"].AsString(), settings.AddressVersion),
                Scopes = (WitnessScope)Enum.Parse(typeof(WitnessScope), u["scopes"]?.AsString()),
                AllowedContracts = ((JArray)u["allowedcontracts"])?.Select(p => UInt160.Parse(p.AsString())).ToArray(),
                AllowedGroups = ((JArray)u["allowedgroups"])?.Select(p => ECPoint.Parse(p.AsString(), ECCurve.Secp256r1)).ToArray()
            }).ToArray())
            {
                Witnesses = _params
                    .Select(u => new
                    {
                        Invocation = u["invocation"]?.AsString(),
                        Verification = u["verification"]?.AsString()
                    })
                    .Where(x => x.Invocation != null || x.Verification != null)
                    .Select(x => new Witness()
                    {
                        InvocationScript = Convert.FromBase64String(x.Invocation ?? string.Empty),
                        VerificationScript = Convert.FromBase64String(x.Verification ?? string.Empty)
                    }).ToArray()
            };

            // Validate format

            _ = IO.Helper.ToByteArray(ret.GetSigners()).AsSerializableArray<Signer>();

            return ret;
        }

        [RpcMethod]
        protected virtual JObject InvokeFunction(JArray _params)
        {
            UInt160 script_hash = UInt160.Parse(_params[0].AsString());
            string operation = _params[1].AsString();
            ContractParameter[] args = _params.Count >= 3 ? ((JArray)_params[2]).Select(p => ContractParameter.FromJson(p)).ToArray() : new ContractParameter[0];
            Signers signers = _params.Count >= 4 ? SignersFromJson((JArray)_params[3], system.Settings) : null;

            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                script = sb.EmitDynamicCall(script_hash, operation, args).ToArray();
            }
            return GetInvokeResult(script, signers);
        }

        [RpcMethod]
        protected virtual JObject InvokeScript(JArray _params)
        {
            byte[] script = Convert.FromBase64String(_params[0].AsString());
            Signers signers = _params.Count >= 2 ? SignersFromJson((JArray)_params[1], system.Settings) : null;
            return GetInvokeResult(script, signers);
        }

        [RpcMethod]
        protected virtual JObject GetUnclaimedGas(JArray _params)
        {
            string address = _params[0].AsString();
            JObject json = new JObject();
            UInt160 script_hash;
            try
            {
                script_hash = AddressToScriptHash(address, system.Settings.AddressVersion);
            }
            catch
            {
                script_hash = null;
            }
            if (script_hash == null)
                throw new RpcException(-100, "Invalid address");
            var snapshot = system.StoreView;
            json["unclaimed"] = NativeContract.NEO.UnclaimedGas(snapshot, script_hash, NativeContract.Ledger.CurrentIndex(snapshot) + 1).ToString();
            json["address"] = script_hash.ToAddress(system.Settings.AddressVersion);
            return json;
        }

        static string GetExceptionMessage(Exception exception)
        {
            return exception?.GetBaseException().Message;
        }
    }
}
