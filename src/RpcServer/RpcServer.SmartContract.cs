#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.IO;
using System.Linq;
using Array = Neo.VM.Types.Array;

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
                var stackItems = engine.ResultStack.ToArray();
                ConvertIEnumeratorToArray(stackItems); // convert InteropInterface<IEnumerator> to Array for RpcClient to digest
                json["stack"] = new JArray(stackItems.Select(p => p.ToJson()));
            }
            catch (InvalidOperationException)
            {
                json["stack"] = "error: recursive reference";
            }
            ProcessInvokeWithWallet(json);
            return json;
        }

        public static void ConvertIEnumeratorToArray(StackItem[] stackItems)
        {
            for (int i = 0; i < stackItems.Length; i++)
            {
                if (stackItems[i] is InteropInterface interopInterface)
                {
                    if (interopInterface.TryGetInterface(out System.Collections.IEnumerator sysEnum))
                    {
                        Array array = new Array();
                        while (sysEnum.MoveNext())
                        {
                            var current = sysEnum.Current;
                            if (!(current is StackItem))
                            {
                                if (current is IInteroperable interoperable)
                                    current = interoperable.ToStackItem(null);
                                else
                                    current = new InteropInterface(current);
                            }
                            array.Add((StackItem)current);
                        }
                        stackItems[i] = array;
                        continue;
                    }
                    if (interopInterface.TryGetInterface(out Neo.SmartContract.Enumerators.IEnumerator neoEnum))
                    {
                        Array array = new Array();
                        while (neoEnum.Next())
                        {
                            array.Add(neoEnum.Value());
                        }
                        stackItems[i] = array;
                    }
                }
            }
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

        [RpcMethod]
        private JObject GetUnclaimedGas(JArray _params)
        {
            string address = _params[0].AsString();
            JObject json = new JObject();
            UInt160 script_hash;
            try
            {
                script_hash = address.ToScriptHash();
            }
            catch
            {
                script_hash = null;
            }
            if (script_hash == null)
                throw new RpcException(-100, "Invalid address");
            SnapshotView snapshot = Blockchain.Singleton.GetSnapshot();
            json["unclaimed"] = NativeContract.NEO.UnclaimedGas(snapshot, script_hash, snapshot.Height + 1).ToString();
            json["address"] = script_hash.ToAddress();
            return json;
        }
    }
}
