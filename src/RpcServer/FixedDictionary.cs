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
            if (dictionary.Count == capacity)
            {
                var oldestKey = keys[0];
                dictionary.Remove(oldestKey);
                keys.RemoveAt(0);
            }

            dictionary.Add(key, value);
            keys.Add(key);
        }

        public void Remove(TKey key)
        {
            dictionary.Remove(key);
            keys.Remove(key);
        }

        public bool ContainsKey(TKey key)
        {
            return keys.Contains(key);
        }

        public TValue this[TKey key]
        {
            get { return dictionary[key]; }
        }
    }
}
