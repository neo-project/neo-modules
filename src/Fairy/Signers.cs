using Neo.Network.P2P.Payloads;
using Neo.Persistence;

namespace Neo.Plugins
{
    class Signers : IVerifiable
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
}
