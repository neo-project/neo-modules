using NeoFS.API.v2.Refs;
using Neo.FSNode.LocalObjectStorage.LocalStore;
using Neo.FSNode.Services.Object.RangeHash.Hasher;
using Neo.FSNode.Utils;
using Neo.Cryptography;
using System;
using System.Collections.Generic;

namespace Neo.FSNode.Services.Object.RangeHash.HasherSource
{
    public class LocalHasherSource : IHasherSource
    {
        private readonly Storage localStorage;

        public void HashRange(RangeHashPrm prm, Action<List<byte[]>> handler)
        {
            var obj = localStorage.Get(prm.Address);
            if (obj is null)
                throw new InvalidOperationException(nameof(LocalHasherSource) + " could not get object from local storage");
            var payload = obj.Payload.ToByteArray();
            var hashes = new List<byte[]>();
            foreach (var range in prm.Ranges)
            {
                var left = (int)range.Offset;
                var right = left + (int)range.Length;
                if (payload.Length < right)
                    throw new InvalidOperationException(nameof(LocalHasherSource) + " range out of bounds");
                var source = payload[left..right].SaltXOR(prm.Salt);
                switch (prm.HashType)
                {
                    case ChecksumType.Sha256:
                        hashes.Add(source.Sha256());
                        break;
                    case ChecksumType.Tz:
                        hashes.Add(source.Tz());
                        break;
                    default:
                        throw new InvalidOperationException(nameof(LocalHasherSource) + " unsupported checksum type");
                }
            }
            handler(hashes);
        }
    }
}
