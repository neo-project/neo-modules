
using System;
using System.Security.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Network;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Storage.Services.Container.Announcement.Route
{
    public class RemoteLoadAnnounceProvider
    {
        public ECDsa Key { get; init; }
        public ILocalInfoSource LocalInfo { get; init; }
        public ClientCache ClientCache { get; init; }
        public IWriterProvider DeadEndProvider { get; init; }

        public IWriterProvider InitRemote(NodeInfo info)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));
            List<Address> addrs = info.Addresses.Select(p => Address.FromString(p)).ToList();
            if (addrs.Intersect(LocalInfo.Addresses).Any())
                return new SimpleProvider(new NopLoadWriter());
            var client = ClientCache.Get(addrs);
            return new RemoteLoadAnnounceWriterProvider
            {
                Key = Key,
                Client = client,
            };
        }
    }
}
