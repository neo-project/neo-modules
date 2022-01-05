using Neo.FileStorage.Cache;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Cache
{
    public class TTLNetworkCache<K, V> where K : IEquatable<K>
    {
        private class ValueWithTime
        {
            public DateTime Expiration;
            public V Value;
        }

        private readonly TimeSpan ttl;
        private readonly Func<K, V> fetcher;
        private readonly LRUCache<K, ValueWithTime> cache;

        public TTLNetworkCache(int capactiy, TimeSpan ttl, Func<K, V> fetcher)
        {
            cache = new(capactiy, null);
            this.ttl = ttl;
            this.fetcher = fetcher;
        }

        public V Get(K key)
        {
            lock (cache)
            {
                if (cache.TryPeek(key, out ValueWithTime vt))
                {
                    if (DateTime.UtcNow < vt.Expiration)
                    {
                        return vt.Value;
                    }
                    cache.Remove(key);
                }
                var value = fetcher(key);
                cache.Add(key, new ValueWithTime { Expiration = DateTime.UtcNow + ttl, Value = value });
                return value;
            }

        }

        public void Remove(K key)
        {
            lock (cache)
            {
                cache.Remove(key);
            }
        }

        public IEnumerable<K> Keys()
        {
            lock (cache)
            {
                return cache.Keys();
            }
        }
    }
}
