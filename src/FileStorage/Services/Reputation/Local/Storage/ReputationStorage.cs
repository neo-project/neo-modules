
using System;
using System.Collections.Concurrent;

namespace Neo.FileStorage.Services.Reputaion.Local.Storage
{
    public class ReputationStorage
    {
        private readonly ConcurrentDictionary<ulong, TrustStorage> store = new();

        public void Update(UpdatePrm prm)
        {
            if (store.TryGetValue(prm.Epoch, out TrustStorage storage))
            {
                storage.Update(prm);
                return;
            }
            storage = new();
            storage.Update(prm);
            store[prm.Epoch] = storage;
        }

        public TrustStorage DataForEpoch(ulong epoch)
        {
            return store[epoch];
        }
    }
}
