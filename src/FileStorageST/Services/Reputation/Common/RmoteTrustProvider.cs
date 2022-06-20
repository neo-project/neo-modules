using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Network;
using Neo.FileStorage.Storage.Services.Util;
using System;
using System.Linq;
using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Reputaion.Common
{
    public class RemoteTrustProvider : IRemoteWriterProvider
    {
        public ECDsa Key { get; init; }
        public ILocalInfoSource LocalInfoSource { get; init; }
        public IWriterProvider DeadEndProvider { get; init; }
        public IFSClientCache ClientCache { get; init; }
        public IClientKeyRemoteProvider RemoteProvider { get; init; }

        public IWriterProvider InitRemote(NodeInfo ni)
        {
            if (ni is null) return DeadEndProvider;
            if (LocalInfoSource.PublicKey.SequenceEqual(ni.PublicKey.ToByteArray()))
                return new SimpleWriterProvider(new NonWriter());
            var client = ClientCache.Get(ni);
            return RemoteProvider.WithClient(client);
        }
    }
}
