using System.Collections.Generic;

namespace Neo.Plugins
{
    public class FixedDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
        private readonly List<TKey> keys = new List<TKey>();
        private readonly int capacity;

        public FixedDictionary(int capacity)
        {
            this.capacity = capacity;
        }

        public void Add(TKey key, TValue value)
        {
            if (dictionary.TryAdd(key, value))
            {
                keys.Add(key);

                if (dictionary.Count >= capacity)
                {
                    var oldestKey = keys[0];
                    dictionary.Remove(oldestKey);
                    keys.RemoveAt(0);
                }
            }
        }

        public bool Remove(TKey key, out TValue value)
        {
            if (dictionary.Remove(key, out value))
            {
                keys.Remove(key);
                return true;
            }
            return false;
        }

        public bool ContainsKey(TKey key)
        {
            return dictionary.ContainsKey(key);
        }

        public TValue this[TKey key]
        {
            get { return dictionary[key]; }
        }
    }
}
