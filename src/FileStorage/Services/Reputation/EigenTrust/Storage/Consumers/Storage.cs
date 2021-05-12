using System.Collections.Concurrent;

namespace Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Consumers
{
    public class Storage
    {
        private readonly ConcurrentDictionary<ulong, IterationConsumersStorage> store = new();

        public void Put(IterationTrust t)
        {
            if (!store.TryGetValue(t.Epoch, out IterationConsumersStorage storage))
            {
                storage = new();
                store[t.Epoch] = storage;
            }
            storage.Put(t);
        }

        public bool Consumers(ulong epoch, uint iter, out ConsumersStorage storage)
        {
            storage = null;
            if (!store.TryGetValue(epoch, out IterationConsumersStorage stor))
            {
                return false;
            }
            return stor.Consumers(iter, out storage);
        }
    }
}
