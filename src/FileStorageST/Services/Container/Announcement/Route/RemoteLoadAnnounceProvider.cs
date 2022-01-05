using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Network;
using Neo.FileStorage.Storage.Services.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Container.Announcement.Route
{
    public class RemoteLoadAnnounceProvider
    {
        public ECDsa Key { get; init; }
        public ILocalInfoSource LocalInfo { get; init; }
        public ClientCache ClientCache { get; init; }
        public IWriterProvider DeadEndProvider { get; init; }

        public IWriterProvider InitRemote(NodeInfo node)
        {
            if (node is null)
                return DeadEndProvider;
            if (LocalInfo.PublicKey.SequenceEqual(node.PublicKey))
                return new SimpleWriteProvider(new NopLoadWriter());
            var client = ClientCache.Get(node);
            return new RemoteLoadAnnounceWriterProvider
            {
                Key = Key,
                Client = client,
            };
        }
    }
}
