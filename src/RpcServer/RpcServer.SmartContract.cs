#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.IO;
using System.Linq;

namespace Neo.Plugins
{
    partial class RpcServer
    {
        private class CheckWitnessHashes : IVerifiable
        {
            private readonly UInt160[] _scriptHashesForVerifying;
            public Witness[] Witnesses { get; set; }
            public int Size { get; }

            public CheckWitnessHashes(UInt160[] scriptHashesForVerifying)
            {
                _scriptHashesForVerifying = scriptHashesForVerifying;
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

            public UInt160[] GetScriptHashesForVerifying(StoreView snapshot)
            {
                return _scriptHashesForVerifying;
            }

            public void SerializeUnsigned(BinaryWriter writer)
            {
                throw new NotImplementedException();
            }
        }

        private JObject GetInvokeResult(byte[] script, IVerifiable checkWitnessHashes = null)
        {
            using ApplicationEngine engine = ApplicationEngine.Run(script, checkWitnessHashes, extraGAS: settings.MaxGasInvoke);
            JObject json = new JObject();
            json["script"] = script.ToHexString();
            json["state"] = engine.State;
            json["gas_consumed"] = engine.GasConsumed.ToString();
            try
            {
                json["stack"] = new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()));
            }
            catch (InvalidOperationException)
            {
                json["stack"] = "error: recursive reference";
            }
            ProcessInvokeWithWallet(json);
            return json;
        }

        [RpcMethod]
        private JObject InvokeFunction(JArray _params)
        {
            UInt160 script_hash = UInt160.Parse(_params[0].AsString());
            string operation = _params[1].AsString();
            ContractParameter[] args = _params.Count >= 3 ? ((JArray)_params[2]).Select(p => ContractParameter.FromJson(p)).ToArray() : new ContractParameter[0];
            CheckWitnessHashes checkWitnessHashes = _params.Count >= 4 ? new CheckWitnessHashes(((JArray)_params[3]).Select(u => UInt160.Parse(u.AsString())).ToArray()) : null;
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                script = sb.EmitAppCall(script_hash, operation, args).ToArray();
            }
            return GetInvokeResult(script, checkWitnessHashes);
        }

        [RpcMethod]
        private JObject InvokeScript(JArray _params)
        {
            byte[] script = _params[0].AsString().HexToBytes();
            CheckWitnessHashes checkWitnessHashes = _params.Count >= 2 ? new CheckWitnessHashes(((JArray)_params[1]).Select(u => UInt160.Parse(u.AsString())).ToArray()) : null;
            return GetInvokeResult(script, checkWitnessHashes);
        }
    }
}
