using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.IO;

namespace Neo.Plugins.StateService.Network
{
    class StateRoot : IVerifiable
    {
        public byte Version;
        public uint Index;
        public UInt256 RootHash;
        public Witness Witness;

        private UInt256 _hash = null;
        public UInt256 Hash
        {
            get
            {
                if (_hash is null)
                {
                    _hash = new UInt256(Crypto.Hash256(this.GetHashData()));
                }
                return _hash;
            }
        }

        Witness[] IVerifiable.Witnesses
        {
            get
            {
                return new[] { Witness };
            }
            set
            {
                if (value.Length != 1) throw new ArgumentException(null, nameof(value));
                Witness = value[0];
            }
        }

        int ISerializable.Size =>
            sizeof(byte) +      //Version
            sizeof(uint) +      //Index
            UInt256.Length +    //RootHash
            1 + Witness.Size;   //Witness

        void ISerializable.Deserialize(BinaryReader reader)
        {
            DeserializeUnsigned(reader);
            Witness[] witnesses = reader.ReadSerializableArray<Witness>(1);
            if (witnesses.Length != 1) throw new FormatException();
            Witness = witnesses[0];
        }

        public void DeserializeUnsigned(BinaryReader reader)
        {
            Version = reader.ReadByte();
            Index = reader.ReadUInt32();
            RootHash = reader.ReadSerializable<UInt256>();
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            SerializeUnsigned(writer);
            writer.Write(new[] { Witness });
        }

        public void SerializeUnsigned(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(Index);
            writer.Write(RootHash);
        }

        public bool Verify(DataCache snapshot)
        {
            return this.VerifyWitnesses(snapshot, 1_00000000);
        }

        public UInt160[] GetScriptHashesForVerifying(DataCache snapshot)
        {
            ECPoint[] validators = NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.StateValidator, Index);
            if (validators.Length < 1) throw new InvalidOperationException("No script hash for state root verifying");
            return new UInt160[] { Contract.GetBFTAddress(validators) };
        }

        public JObject ToJson()
        {
            var json = new JObject();
            json["version"] = Version;
            json["index"] = Index;
            json["roothash"] = RootHash.ToString();
            json["witnesses"] = new JArray(Witness.ToJson());
            return json;
        }
    }
}
