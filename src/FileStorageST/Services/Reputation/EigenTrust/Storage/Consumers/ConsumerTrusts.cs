using System;
using System.Collections.Concurrent;
using Neo.FileStorage.API.Reputation;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Consumers
{
    public class ConsumerTrusts
    {
        private readonly ConcurrentDictionary<PeerID, PeerToPeerTrust> store = new();

        public void Put(IterationTrust t)
        {
            store[t.Trust.TrustingPeer] = t.Trust;
        }

        public void Iterate(Action<PeerToPeerTrust> handler)
        {
            foreach (var t in store.Values)
                handler(t);
        }
    }
}
