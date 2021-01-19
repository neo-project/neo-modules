using NeoFS.API.v2.Acl;
using V2Range = NeoFS.API.v2.Object.Range;
using V2Address = NeoFS.API.v2.Refs.Address;
using NeoFS.API.v2.Session;
using Neo.FSNode.LocalObjectStorage.LocalStore;
using Neo.FSNode.Network;
using Neo.FSNode.Network.Cache;
using Neo.FSNode.Services.Object.Util;
using System;

namespace Neo.FSNode.Services.Object.Range.RangeSource
{
    public class RemoteRangeSource : IRangeSource
    {
        private readonly Storage localStorage;
        private readonly KeyStorage keyStorage;
        private readonly SessionToken sessionToken;
        private readonly BearerToken bearerToken;
        private readonly ClientCache clientCache;
        private readonly Address node;

        public RemoteRangeSource(Address addr)
        {
            node = addr;
        }

        public byte[] Range(V2Address address, V2Range range)
        {
            var key = keyStorage.GetKey(sessionToken);
            if (key is null)
                throw new InvalidOperationException(nameof(Range) + " could not receive private key");
            var addr = node.IPAddressString();
            var client = clientCache.GetClient(key, addr);
            if (client is null)
                throw new InvalidOperationException(nameof(Range) + $" could not create SDK client {addr}");
            var data = client.GetObjectPayloadRangeData(address, range, new NeoFS.API.v2.Client.CallOptions
            {
                Ttl = 1,
                Session = sessionToken,
                Bearer = bearerToken,
            }).Result;
            if (data is null)
                throw new InvalidOperationException(nameof(Range) + $" could not read object payload range from {addr}");
            return data;
        }
    }
}
