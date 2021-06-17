using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.Morph.Invoker;
using System;
using System.Linq;

namespace Neo.FileStorage.InnerRing
{
    public class InnerRingIndexer
    {
        private Object lockObject = new();
        private Indexes ind = new();
        private DateTime lastAccess = DateTime.Now;
        private Client client;
        private TimeSpan timeout;

        public InnerRingIndexer(Client client, TimeSpan to)
        {
            this.client = client;
            this.timeout = to;
        }

        public Indexes Update()
        {
            lock (lockObject)
            {
                if (DateTime.Now.Subtract(lastAccess) < timeout) return ind;
                var key = client.GetWallet().GetAccounts().ToArray()[0].GetKey().PublicKey;
                client.InnerRingIndex(key, out int innerRingIndex, out int innerRingSize);
                ind.innerRingIndex = innerRingIndex;
                ind.innerRingSize = innerRingSize;
                ind.alphabetIndex = ContractInvoker.AlphabetIndex(client, key);
                lastAccess = DateTime.Now;
                return ind;
            }
        }

        public int InnerRingIndex()
        {
            Indexes ind = Update();
            return ind.innerRingIndex;
        }

        public int InnerRingSize()
        {
            Indexes ind = Update();
            return ind.innerRingSize;
        }

        public int AlphabetIndex()
        {
            Indexes ind = Update();
            return ind.alphabetIndex;
        }

        public class Indexes
        {
            public int innerRingIndex;
            public int innerRingSize;
            public int alphabetIndex;
        }

    }
}
