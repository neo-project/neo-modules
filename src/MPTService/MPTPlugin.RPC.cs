using Akka.Actor;
using Neo.IO;
using Neo.IO.Json;
using Neo.Cryptography.MPT;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.MPTService.MPTStorage;
using Neo.Plugins.MPTService.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Neo.Plugins.MPTService
{
    public partial class MPTPlugin
    {
        [RpcMethod]
        public JObject GetStateRoot(JArray _params)
        {
            uint index = uint.Parse(_params[0].AsString());
            StateRoot state_root = MPTStore.Singleton.StateRoots.TryGet(index);
            if (state_root is null)
                throw new RpcException(-100, "Unknown root hash");
            else
                return state_root.ToJson();
        }

        [RpcMethod]
        public JObject GetProof(JArray _params)
        {
            UInt256 root_hash = UInt256.Parse(_params[0].AsString());
            if (!Settings.Default.FullState)
            {
                if (MPTStore.Singleton.CurrentLocalRootHash != root_hash) throw new RpcException(-100, "Unknown root hash");
            }
            UInt160 script_hash = UInt160.Parse(_params[1].AsString());
            byte[] key = _params[2].AsString().HexToBytes();
            var contract = Blockchain.Singleton.View.Contracts.TryGet(script_hash);
            if (contract is null) throw new RpcException(-100, "Unknown contract");
            StorageKey skey = new StorageKey
            {
                Id = contract.Id,
                Key = key,
            };
            HashSet<byte[]> proof = MPTStore.Singleton.GetProof(root_hash, skey);
            if (proof is null) throw new RpcException(-100, "Unknown value");

            using MemoryStream ms = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8);

            writer.WriteVarBytes(skey.ToArray());
            writer.WriteVarInt(proof.Count);
            foreach (var item in proof)
            {
                writer.WriteVarBytes(item);
            }
            writer.Flush();

            return ms.ToArray().ToHexString();
        }

        [RpcMethod]
        public JObject VerifyProof(JArray _params)
        {
            UInt256 root_hash = UInt256.Parse(_params[0].AsString());
            byte[] proof_bytes = _params[1].AsString().HexToBytes();
            var proof = new HashSet<byte[]>();

            using MemoryStream ms = new MemoryStream(proof_bytes, false);
            using BinaryReader reader = new BinaryReader(ms, Encoding.UTF8);

            var key = reader.ReadVarBytes(MPTNode.MaxKeyLength);
            var count = reader.ReadVarInt();
            for (ulong i = 0; i < count; i++)
            {
                proof.Add(reader.ReadVarBytes());
            }

            var skey = key.AsSerializable<StorageKey>();
            var sitem = MPTTrie<StorageKey, StorageItem>.VerifyProof(root_hash, skey, proof);
            if (sitem is null) throw new RpcException(-100, "Verification failed");
            return sitem.Value.ToHexString();
        }

        [RpcMethod]
        public JObject VoteStateRoot(JArray _params)
        {
            uint height = uint.Parse(_params[0].AsString());
            int validator_index = int.Parse(_params[1].AsString());
            byte[] sig = _params[2].AsString().HexToBytes();
            Validation?.Tell(new Vote(height, validator_index, sig));
            return true;
        }
    }
}
