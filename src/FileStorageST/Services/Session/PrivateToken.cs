using Neo.FileStorage.API.Cryptography;
using Neo.IO;
using System;
using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Session
{
    public class PrivateToken : ISerializable
    {
        public ECDsa SessionKey;
        public ulong Expiration;

        public int Size => 32 + sizeof(ulong);

        void ISerializable.Serialize(System.IO.BinaryWriter writer)
        {
            writer.Write(SessionKey.PrivateKey());
            writer.Write(BitConverter.GetBytes(Expiration));
        }

        void ISerializable.Deserialize(System.IO.BinaryReader reader)
        {
            SessionKey = reader.ReadBytes(32).LoadPrivateKey();
            Expiration = BitConverter.ToUInt64(reader.ReadBytes(sizeof(ulong)));
        }
    }
}
