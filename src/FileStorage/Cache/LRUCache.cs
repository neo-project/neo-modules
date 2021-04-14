using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Neo.FileStorage.Cache
{
    public class LRUCache<K, V> where K : IEquatable<K>
    {
        private class Element
        {
            public K Key;
            public V Value;

            public Element(K k, V v)
            {
                Key = k;
                Value = v;
            }
        }

        private readonly Dictionary<K, Element> cache = new ();
        private readonly LinkedList<Element> list = new ();
        private readonly int capacity;
        private readonly Action<K, V> onEvict;
        public int Count => cache.Count;

        public LRUCache(int cap, Action<K, V> evict = null)
        {
            capacity = cap;
            onEvict = evict;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGet(K key, out V value)
        {
            if (cache.TryGetValue(key, out Element el))
            {
                list.Remove(el);
                list.AddFirst(el);
                value = el.Value;
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryPeek(K key, out V value)
        {
            if (cache.TryGetValue(key, out Element el))
            {
                value = el.Value;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryAdd(K key, V value)
        {
            Element evicted_el = null;
            lock (this)
            {
                if (cache.TryGetValue(key, out Element el))
                {
                    list.Remove(el);
                    list.AddFirst(el);
                    return true;
                }
                el = new (key, value);
                cache.Add(key, el);
                list.AddFirst(el);
                if (capacity < cache.Count)
                {
                    evicted_el = list.Last.Value;
                    cache.Remove(evicted_el.Key);
                    list.RemoveLast();
                }
            }
            if (onEvict is not null && evicted_el is not null)
                onEvict(evicted_el.Key, evicted_el.Value);
            return true;
        }

        public bool Remove(K key)
        {
            bool evicted;
            Element evicted_e = null;
            lock (this)
            {
                evicted = cache.Remove(key, out evicted_e) && list.Remove(evicted_e);
            }
            if (onEvict is not null && evicted)
                onEvict(evicted_e.Key, evicted_e.Value);
            return evicted;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Contains(K key)
        {
            return cache.ContainsKey(key);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<K> Keys()
        {
            foreach (var n in list)
                yield return n.Key;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Purge()
        {
            foreach (var item in cache.Values)
            {
                if (onEvict is not null)
                    onEvict(item.Key, item.Value);
            }
            cache.Clear();
            list.Clear();
        }
    }
}
