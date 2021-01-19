using NeoFS.API.v2.Refs;
using Neo.FSNode.Network;
using Neo.FSNode.Network.Cache;
using Neo.FSNode.Services.Object.Util;
using System;
using System.Collections.Generic;

namespace Neo.FSNode.Services.Object.RangeHash.HasherSource
{
    public class RemoteHasherSource : IHasherSource
    {
        private readonly KeyStorage keyStorage;
        private readonly ClientCache clientCache;
        private readonly Network.Address node;

        public void HashRange(RangeHashPrm prm, Action<List<byte[]>> handler)
        {
            if (prm.HashType != ChecksumType.Sha256 && prm.HashType != ChecksumType.Tz)
                throw new InvalidOperationException(nameof(RemoteHasherSource) + " unsupported checksum type");
            var key = keyStorage.GetKey(prm.SessionToken);
            if (key is null)
                throw new InvalidOperationException(nameof(RemoteHasherSource) + " could not receive private key");
            var addr = node.IPAddressString();
            var client = clientCache.GetClient(key, addr);
            if (client is null)
                throw new InvalidOperationException(nameof(RemoteHasherSource) + $" could not create SDK client {addr}");
            var hashes = client.GetObjectPayloadRangeHash(prm.Address, prm.Ranges.ToArray(), prm.Salt, prm.HashType, new NeoFS.API.v2.Client.CallOptions
            {
                Ttl = 1,
                Session = prm.SessionToken,
                Bearer = prm.BearerToken,
            });
            if (hashes is null)
                throw new InvalidOperationException(nameof(RemoteHasherSource) + $" could not read range hash from {addr}");
            handler(hashes);
        }
    }
}
