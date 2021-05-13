using System.Collections.Concurrent;

namespace Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Consumers
{
    public class IterationConsumersStorage
    {
        private readonly ConcurrentDictionary<uint, ConsumersStorage> store = new();

        public void Put(IterationTrust t)
        {
            if (!store.TryGetValue(t.Index, out ConsumersStorage storage))
            {
                storage = new();
                store[t.Index] = storage;
            }
            storage.Put(t);
        }

        public bool Consumers(uint iter, out ConsumersStorage storage)
        {
            return store.TryGetValue(iter, out storage);
        }
    }
}
