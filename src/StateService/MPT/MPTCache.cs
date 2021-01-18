using Neo.IO;
using Neo.Persistence;
using System.Collections.Generic;

namespace Neo.Plugins.MPT
{
    public class MPTCache
    {
        private enum TrackState : byte
        {
            None,
            Added,
            Changed,
            Deleted
        }

        private class Trackable
        {
            public MPTNode Node;
            public TrackState State;
        }

        private readonly ISnapshot store;
        private readonly byte prefix;
        private readonly Dictionary<UInt256, Trackable> cache = new Dictionary<UInt256, Trackable>();

        public MPTCache(ISnapshot store, byte prefix)
        {
            this.store = store;
            this.prefix = prefix;
        }

        public MPTNode Resolve(UInt256 hash)
        {
            if (cache.TryGetValue(hash, out Trackable t))
            {
                return t.Node?.Clone();
            }
            var n = store.TryGet(prefix, hash.ToArray())?.AsSerializable<MPTNode>();
            cache.Add(hash, new Trackable
            {
                Node = n,
                State = TrackState.None,
            });
            return n?.Clone();
        }

        public void PutNode(MPTNode np)
        {
            var n = Resolve(np.Hash);
            if (n is null)
            {
                np.Reference = 1;
                cache[np.Hash] = new Trackable
                {
                    Node = np.Clone(),
                    State = TrackState.Added,
                };
                return;
            }
            var entry = cache[np.Hash];
            entry.Node.Reference++;
            entry.State = TrackState.Changed;
        }

        public void DeleteNode(UInt256 hash)
        {
            var n = Resolve(hash);
            if (n is null) return;
            if (1 < n.Reference)
            {
                var entry = cache[hash];
                entry.Node.Reference--;
                entry.State = TrackState.Changed;
                return;
            }
            cache[hash] = new Trackable
            {
                Node = null,
                State = TrackState.Deleted,
            };
        }

        public void Commit()
        {
            foreach (var item in cache)
            {
                switch (item.Value.State)
                {
                    case TrackState.Added:
                    case TrackState.Changed:
                        store.Put(prefix, item.Key.ToArray(), item.Value.Node.ToArray());
                        break;
                    case TrackState.Deleted:
                        store.Delete(prefix, item.Key.ToArray());
                        break;
                }
            }
            cache.Clear();
        }
    }
}
