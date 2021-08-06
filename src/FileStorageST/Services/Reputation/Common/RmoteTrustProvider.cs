using System.Security.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Network;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Storage.Services.Reputaion.Common
{
    public class RemoteTrustProvider : IRemoteWriterProvider
    {
        public ECDsa Key { get; init; }
        public ILocalInfoSource LocalAddressesSource { get; init; }
        public IWriterProvider DeadEndProvider { get; init; }
        public IFSClientCache ClientCache { get; init; }
        public IClientKeyRemoteProvider RemoteProvider { get; init; }

        public IWriterProvider InitRemote(NodeInfo ni)
        {
            if (ni is null) return DeadEndProvider;
            var addresses = ni.Addresses.Select(p => Address.FromString(p)).ToList();
            if (LocalAddressesSource.Addresses.Intersect(addresses).Any())
                return new SimpleWriterProvider(new NonWriter());
            var client = ClientCache.Get(addresses);
            return RemoteProvider.WithClient(client);
        }
    }
}
