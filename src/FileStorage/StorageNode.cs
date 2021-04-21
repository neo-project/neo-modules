using Neo.FileStorage.API.Netmap;
using System;
using System.Security.Cryptography;

namespace Neo.FileStorage
{
    public sealed class StorageNode : IDisposable
    {
        public ProtocolSettings ProtocolSettings;
        private ECDsa key;
        public ulong CurrentEpoch;
        public NodeInfo LocalNodeInfo;

        public ECDsa Key => key;

        public void Dispose()
        {
            key.Dispose();
        }
    }
}
