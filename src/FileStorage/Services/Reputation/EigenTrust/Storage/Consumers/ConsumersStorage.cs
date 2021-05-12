using System;
using System.Collections.Concurrent;

namespace Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Consumers
{
    public class ConsumersStorage
    {
        private readonly ConcurrentDictionary<PeerID, ConsumersTrusts> store = new();

        public void Put(IterationTrust t)
        {
            if (!store.TryGetValue(t.Trust.Peer, out ConsumersTrusts storage))
            {
                storage = new();
                store[t.Trust.Peer] = storage;
            }
            storage.Put(t);
        }

        public void Iterate(Action<PeerID, ConsumersTrusts> handler)
        {
            foreach (var (key, value) in store)
            {
                try
                {
                    handler(key, value);
                }
                catch
                {
                    break;
                }
            }
        }
    }
}
