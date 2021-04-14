using Neo.FileStorage.API.Netmap;
using System;
using System.Security.Cryptography;

namespace Neo.FileStorage
{
    public sealed class StorageNode : IDisposable
    {
        public ProtocolSettings ProtocolSettings;
        private ECDsa key;
        private ulong epoch;
        private NodeInfo localNodeInfo;

        public ECDsa Key => key;
        public ulong CurrentEpoch => epoch;
        public NodeInfo LocalNodeInfo => localNodeInfo;

        public void Dispose()
        {
            key.Dispose();
        }
    }
}
