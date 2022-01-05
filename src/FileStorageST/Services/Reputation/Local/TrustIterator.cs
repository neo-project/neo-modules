using Google.Protobuf;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Storage.Services.Reputaion.Common;
using Neo.FileStorage.Storage.Services.Reputaion.Local.Storage;
using Neo.Network.P2P;
using System;
using System.Linq;

namespace Neo.FileStorage.Storage.Services.Reputaion.Local
{
    public class TrustIterator : IIterator
    {
        public ICommonContext Context { get; init; }
        public LocalTrustStorage LocalTrustStorage { get; init; }
        public EpochTrustStorage EpochTrustStorage { get; init; }

        public void Iterate(Action<PeerToPeerTrust> handler)
        {
            if (EpochTrustStorage is not null)
            {
                EpochTrustStorage.Iterate(t =>
                {
                    PeerToPeerTrust trust = new();
                    trust.Trust = t;
                    trust.TrustingPeer = LocalTrustStorage.LocalKey;
                    handler(trust);
                });
                return;
            }
            var nm = LocalTrustStorage.NetmapCache.GetNetMapByEpoch(Context.Epoch);
            int lindex = -1;
            for (int i = 0; i < nm.Nodes.Count; i++)
                if (nm.Nodes[i].PublicKey.SequenceEqual(LocalTrustStorage.LocalKey))
                    lindex = i;
            int len = nm.Nodes.Count;
            if (0 <= lindex && 0 < nm.Nodes.Count)
                len--;
            double p = 1.0 / len;
            for (int i = 0; i < nm.Nodes.Count; i++)
            {
                if (i == lindex) continue;
                PeerToPeerTrust t = new()
                {
                    TrustingPeer = LocalTrustStorage.LocalKey,
                    Trust = new()
                    {
                        Peer = nm.Nodes[i].PublicKey,
                        Value = p,
                    },
                };
                handler(t);
            }
        }
    }
}
