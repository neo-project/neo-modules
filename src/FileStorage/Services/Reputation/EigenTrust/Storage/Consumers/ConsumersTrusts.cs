using System;
using System.Collections.Concurrent;

namespace Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Consumers
{
    public class ConsumersTrusts
    {
        private readonly ConcurrentDictionary<PeerID, Trust> store = new();

        public void Put(IterationTrust t)
        {
            store[t.Trust.Trusting] = t.Trust;
        }

        public void Iterate(Action<Trust> handler)
        {
            foreach (var t in store.Values)
                handler(t);
        }
    }
}
