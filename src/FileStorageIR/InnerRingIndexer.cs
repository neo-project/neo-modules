using System;
using System.Linq;
using Neo.FileStorage.Invoker.Morph;

namespace Neo.FileStorage.InnerRing
{
    public class InnerRingIndexer
    {
        private readonly object lockObject = new();
        private readonly Indexes ind = new();
        private DateTime lastAccess = DateTime.Now;
        private readonly MorphInvoker morphInvoker;
        private readonly TimeSpan timeout;

        public InnerRingIndexer(MorphInvoker invoker, TimeSpan to)
        {
            this.morphInvoker = invoker;
            this.timeout = to;
        }

        public Indexes Update()
        {
            lock (lockObject)
            {
                if (DateTime.Now.Subtract(lastAccess) < timeout) return ind;
                var key = morphInvoker.Wallet.GetAccounts().ToArray()[0].GetKey().PublicKey;
                morphInvoker.InnerRingIndex(key, out int innerRingIndex, out int innerRingSize);
                ind.InnerRingIndex = innerRingIndex;
                ind.InnerRingSize = innerRingSize;
                ind.AlphabetIndex = morphInvoker.AlphabetIndex(key);
                lastAccess = DateTime.Now;
                return ind;
            }
        }

        public int InnerRingIndex()
        {
            Indexes ind = Update();
            return ind.InnerRingIndex;
        }

        public int InnerRingSize()
        {
            Indexes ind = Update();
            return ind.InnerRingSize;
        }

        public int AlphabetIndex()
        {
            Indexes ind = Update();
            return ind.AlphabetIndex;
        }

        public class Indexes
        {
            public int InnerRingIndex;
            public int InnerRingSize;
            public int AlphabetIndex;
        }

    }
}
