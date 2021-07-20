using System;
using System.Collections.Concurrent;
using Neo.FileStorage.API.Reputation;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Daughters
{
    public class DaughterTrusts
    {
        private readonly ConcurrentDictionary<PeerID, PeerToPeerTrust> store = new();

        public void Put(PeerToPeerTrust t)
        {
            store[t.Trust.Peer] = t;
        }

        public void Iterate(Action<PeerToPeerTrust> handler)
        {
            foreach (var t in store.Values)
                handler(t);
        }
    }
}
