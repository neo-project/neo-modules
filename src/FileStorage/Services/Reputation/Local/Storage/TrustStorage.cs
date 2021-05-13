using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Services.Reputaion.Local.Storage
{
    public class TrustStorage
    {
        private readonly ConcurrentDictionary<string, TrustValue> store = new();

        public void Update(UpdatePrm prm)
        {
            string key = prm.PeerId.ToString();
            if (store.TryGetValue(key, out TrustValue value))
            {
                if (prm.Sat) value.Sat++;
                value.All++;
                return;
            }
            value = new()
            {
                Sat = prm.Sat ? 1 : 0,
                All = 1,
            };
            store[key] = value;
        }


        public void Iterate(Action<Trust> handler)
        {
            double sum = 0;
            Dictionary<string, double> values = new();
            foreach (var (id, tv) in store)
            {
                if (0 < tv.All)
                {
                    double num = (double)tv.Sat;
                    double denom = (double)tv.All;
                    double value = num / denom;
                    values[id] = value;
                    sum += value;
                }
            }
            if (sum != 0)
            {
                foreach (var trust in values.Select(p => new Trust { Peer = PeerID.FromString(p.Key), Value = p.Value }))
                {
                    handler(trust);
                }
            }
        }
    }
}
