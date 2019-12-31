#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Microsoft.AspNetCore.Mvc;
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
    partial class RestController
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

        /// <summary>
        /// Invoke a smart contract with specified script hash, passing in an operation and the corresponding params	
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        [HttpPost("contracts/invokefunction")]
        public IActionResult InvokeFunction(InvokeFunctionParameter param)
        {
            UInt160 script_hash = UInt160.Parse(param.ScriptHash);
            string operation = param.Operation;
            ContractParameter[] args = param.Params?.Select(p => ContractParameter.FromJson(p.ToJson()))?.ToArray() ?? new ContractParameter[0];
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                script = sb.EmitAppCall(script_hash, operation, args).ToArray();
            }
            return FormatJson(GetInvokeResult(script));
        }

        /// <summary>
        /// Run a script through the virtual machine and get the result
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        [HttpPost("contracts/invokescript")]
        public IActionResult InvokeScript(InvokeScriptParameter param)
        {
                byte[] script = param.Script.HexToBytes();
                CheckWitnessHashes checkWitnessHashes = null;
                if (param.Hashes != null && param.Hashes.Length > 0)
                {
                    UInt160[] scriptHashesForVerifying = param.Hashes.Select(u => UInt160.Parse(u)).ToArray(); ;
                    checkWitnessHashes = new CheckWitnessHashes(scriptHashesForVerifying);
                }
                return FormatJson(GetInvokeResult(script, checkWitnessHashes));
        }

        private JObject GetInvokeResult(byte[] script, IVerifiable checkWitnessHashes = null)
        {
            using ApplicationEngine engine = ApplicationEngine.Run(script, checkWitnessHashes, extraGAS: RestSettings.Default.MaxGasInvoke);
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
    }
}
