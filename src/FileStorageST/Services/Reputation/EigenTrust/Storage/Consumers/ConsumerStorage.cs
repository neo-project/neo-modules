using System;
using System.Collections.Concurrent;
using Neo.FileStorage.API.Reputation;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Consumers
{
    public class ConsumerStorage
    {
        private readonly ConcurrentDictionary<PeerID, ConsumerTrusts> store = new();

        public void Put(IterationTrust t)
        {
            if (!store.TryGetValue(t.Trust.Trust.Peer, out ConsumerTrusts storage))
            {
                storage = new();
                store[t.Trust.Trust.Peer] = storage;
            }
            storage.Put(t);
        }

        public void Iterate(Action<PeerID, ConsumerTrusts> handler)
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
