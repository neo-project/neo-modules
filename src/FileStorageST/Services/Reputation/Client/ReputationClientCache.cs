using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Reputation;
using Neo.FileStorage.Storage.Services.Reputaion.Local.Storage;
using Neo.FileStorage.Storage.Services.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Utility;

namespace Neo.FileStorage.Storage.Services.Reputaion.Client
{
    public class ReputationClientCache : IFSClientCache
    {
        public IEpochSource EpochSource { get; init; }
        public INetmapSource NetmapSource { get; init; }
        public TrustStorage ReputationStorage { get; init; }
        public ClientCache ClientCache { get; init; }

        public IFSClient Get(NodeInfo node)
        {
            var client = ClientCache.Get(node);
            try
            {
                var nm = NetmapSource.GetNetMapByDiff(0);
                UpdatePrm prm = new(new(node.PublicKey.ToByteArray()));
                return new ReputationClient()
                {
                    ClientCache = this,
                    FSClient = client,
                    Prm = prm,
                };
            }
            catch (Exception e)
            {
                Log(nameof(ReputationClientCache), LogLevel.Warning, $"could not get netmap, error={e.Message}");
            }
            return new ReputationClient()
            {
                FSClient = client,
                Prm = null,
            };
        }

        public void Dispose() { }
    }
}
