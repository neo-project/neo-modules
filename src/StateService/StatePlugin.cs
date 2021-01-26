using Akka.Actor;
using Neo.ConsoleService;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.MPT;
using Neo.Plugins.StateService.Storage;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins.StateService
{
    public class StatePlugin : Plugin, IPersistencePlugin
    {
        public const string StatePayloadCategory = "StateService";
        public override string Name => "StateService";
        public override string Description => "Enables MPT for the node";

        private IActorRef store;

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnPluginsLoaded()
        {
            store = System.ActorSystem.ActorOf(StateStore.Props(System, Settings.Default.Path));
        }

        public override void Dispose()
        {
            base.Dispose();
            System.EnsureStoped(store);
        }

        void IPersistencePlugin.OnPersist(Block block, DataCache snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            StateStore.Singleton.UpdateLocalStateRoot(block.Index, snapshot.GetChangeSet().Where(p => p.State != TrackState.None).ToList());
        }

        [ConsoleCommand("state root", Category = "StateService", Description = "Get state root by index")]
        private void OnGetStateRoot(uint index)
        {
            using var snapshot = StateStore.Singleton.GetSnapshot();
            StateRoot state_root = snapshot.GetStateRoot(index);
            if (state_root is null)
                Console.WriteLine("Unknown state root");
            else
                Console.WriteLine(state_root.ToJson());
        }

        [ConsoleCommand("state height", Category = "StateService", Description = "Get current state root index")]
        private void OnGetStateHeight()
        {
            Console.WriteLine($"LocalRootIndex: {StateStore.Singleton.LocalRootIndex}, ValidatedRootIndex: {StateStore.Singleton.ValidatedRootIndex}");
        }

        [ConsoleCommand("get proof", Category = "StateService", Description = "Get proof of key and contract hash")]
        private void OnGetProof(UInt256 root_hash, UInt160 script_hash, string key)
        {
            try
            {
                Console.WriteLine(GetProof(root_hash, script_hash, Convert.FromBase64String(key)));
            }
            catch (RpcException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        [ConsoleCommand("verify proof", Category = "StateService", Description = "Verify proof, return value if successed")]
        private void OnVerifyProof(UInt256 root_hash, string proof)
        {
            try
            {
                Console.WriteLine(VerifyProof(root_hash, Convert.FromBase64String(proof)));
            }
            catch (RpcException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        [RpcMethod]
        public JObject GetStateRoot(JArray _params)
        {
            uint index = uint.Parse(_params[0].AsString());
            using var snapshot = StateStore.Singleton.GetSnapshot();
            StateRoot state_root = snapshot.GetStateRoot(index);
            if (state_root is null)
                throw new RpcException(-100, "Unknown state root");
            else
                return state_root.ToJson();
        }

        private string GetProof(UInt256 root_hash, UInt160 script_hash, byte[] key)
        {
            if (!Settings.Default.FullState && StateStore.Singleton.CurrentLocalRootHash != root_hash)
            {
                throw new RpcException(-100, "Old state not supported");
            }
            using var snapshot = Singleton.GetSnapshot();
            var contract = NativeContract.ContractManagement.GetContract(snapshot, script_hash);
            if (contract is null) throw new RpcException(-100, "Unknown contract");
            StorageKey skey = new StorageKey
            {
                Id = contract.Id,
                Key = key,
            };
            HashSet<byte[]> proof = StateStore.Singleton.GetProof(root_hash, skey);
            if (proof is null) throw new RpcException(-100, "Unknown value");

            using MemoryStream ms = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(ms, Utility.StrictUTF8);

            writer.WriteVarBytes(skey.ToArray());
            writer.WriteVarInt(proof.Count);
            foreach (var item in proof)
            {
                writer.WriteVarBytes(item);
            }
            writer.Flush();

            return Convert.ToBase64String(ms.ToArray());
        }

        [RpcMethod]
        public JObject GetProof(JArray _params)
        {
            UInt256 root_hash = UInt256.Parse(_params[0].AsString());
            UInt160 script_hash = UInt160.Parse(_params[1].AsString());
            byte[] key = Convert.FromBase64String(_params[2].AsString());
            return GetProof(root_hash, script_hash, key);
        }

        private string VerifyProof(UInt256 root_hash, byte[] proof)
        {
            var proofs = new HashSet<byte[]>();

            using MemoryStream ms = new MemoryStream(proof, false);
            using BinaryReader reader = new BinaryReader(ms, Utility.StrictUTF8);

            var key = reader.ReadVarBytes(MPTNode.MaxKeyLength);
            var count = reader.ReadVarInt();
            for (ulong i = 0; i < count; i++)
            {
                proofs.Add(reader.ReadVarBytes());
            }

            var skey = key.AsSerializable<StorageKey>();
            var sitem = MPTTrie<StorageKey, StorageItem>.VerifyProof(root_hash, skey, proofs);
            if (sitem is null) throw new RpcException(-100, "Verification failed");
            return Convert.ToBase64String(sitem.Value);
        }

        [RpcMethod]
        public JObject VerifyProof(JArray _params)
        {
            UInt256 root_hash = UInt256.Parse(_params[0].AsString());
            byte[] proof_bytes = Convert.FromBase64String(_params[1].AsString());
            return VerifyProof(root_hash, proof_bytes);
        }

        [RpcMethod]
        public JObject GetStateHeight(JArray _params)
        {
            var json = new JObject();
            json["localrootindex"] = StateStore.Singleton.LocalRootIndex;
            json["validatedrootindex"] = StateStore.Singleton.ValidatedRootIndex;
            return json;
        }
    }
}
