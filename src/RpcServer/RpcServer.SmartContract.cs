#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Neo.IO.Json;
using Neo.Ledger;
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
        private class Cosigners : IVerifiable
        {
            private readonly Cosigner[] _cosigners;
            public Witness[] Witnesses { get; set; }
            public int Size { get; }

            public Cosigners(Cosigner[] cosigners)
            {
                _cosigners = cosigners;
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
                return _cosigners.Select(p => p.Account).ToArray();
            }

            public Cosigner[] GetCosigners()
            {
                return _cosigners;
            }

            public void SerializeUnsigned(BinaryWriter writer)
            {
                throw new NotImplementedException();
            }
        }

        private JObject GetInvokeResult(byte[] script, Cosigners cosigners = null)
        {
            using ApplicationEngine engine = ApplicationEngine.Run(script, cosigners, extraGAS: settings.MaxGasInvoke);
            JObject json = new JObject();
            json["script"] = script.ToHexString();
            json["state"] = engine.State;
            json["gasconsumed"] = engine.GasConsumed.ToString();
            try
            {
                json["stack"] = new JArray(engine.ResultStack.Select(p => p.ToJson()));
            }
            catch (InvalidOperationException)
            {
                json["stack"] = "error: recursive reference";
            }
            ProcessInvokeWithWallet(json, cosigners);
            return json;
        }

        [RpcMethod]
        private JObject InvokeFunction(JArray _params)
        {
            UInt160 script_hash = UInt160.Parse(_params[0].AsString());
            string operation = _params[1].AsString();
            ContractParameter[] args = _params.Count >= 3 ? ((JArray)_params[2]).Select(p => ContractParameter.FromJson(p)).ToArray() : new ContractParameter[0];
            Cosigners cosigners = _params.Count >= 4 ? new Cosigners(((JArray)_params[3]).Select(u => new Cosigner() { Account = UInt160.Parse(u["account"].AsString()), Scopes = (WitnessScope)Enum.Parse(typeof(WitnessScope), u["scopes"].AsString()) }).ToArray()) : null;
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                script = sb.EmitAppCall(script_hash, operation, args).ToArray();
            }
            return GetInvokeResult(script, cosigners);
        }

        [RpcMethod]
        private JObject InvokeScript(JArray _params)
        {
            byte[] script = _params[0].AsString().HexToBytes();
            Cosigners cosigners = _params.Count >= 2 ? new Cosigners(((JArray)_params[1]).Select(u => new Cosigner() { Account = UInt160.Parse(u["account"].AsString()), Scopes = (WitnessScope)Enum.Parse(typeof(WitnessScope), u["scopes"].AsString()) }).ToArray()) : null;
            return GetInvokeResult(script, cosigners);
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
