using System.Collections.Generic;
using System.Linq;
using Neo.Network.P2P.Payloads;

namespace Neo.Plugins
{
    public class NotaryRequestPool
    {
        private readonly int cap;
        private readonly NeoSystem neoSystem;
        private FallbackVerificationContext context = new();
        private readonly Dictionary<UInt256, List<UInt256>> tasks = new();
        private readonly Dictionary<UInt256, NotaryRequest> requests = new();
        private readonly SortedSet<NotaryRequest> sortedRequests = new(new NotaryRequestComparer());

        public NotaryRequestPool(NeoSystem system, int capacity)
        {
            neoSystem = system;
            cap = capacity;
        }

        public bool TryAdd(NotaryRequest request, out NotaryRequest removed)
        {
            removed = null;
            if (requests.ContainsKey(request.FallbackTransaction.Hash)) return false;
            if (!request.VerifyStateDependent(neoSystem.Settings, neoSystem.StoreView)) return false;
            if (!context.CheckFallback(request.FallbackTransaction, neoSystem.StoreView)) return false;
            if (!CheckConflicts(request)) return false;
            context.AddFallback(request.FallbackTransaction);
            requests[request.FallbackTransaction.Hash] = request;
            sortedRequests.Add(request);
            if (tasks.TryGetValue(request.MainTransaction.Hash, out var fallbacks))
                fallbacks.Add(request.FallbackTransaction.Hash);
            else
                tasks[request.MainTransaction.Hash] = new() { request.FallbackTransaction.Hash };
            if (requests.Count > cap) removed = RemoveOverCapacity();
            return true;
        }

        private bool CheckConflicts(NotaryRequest request)
        {
            var fallbackTx = request.FallbackTransaction;
            var mainTx = request.MainTransaction;
            foreach (var r in tasks.Values)
            {
                var tx = requests[r.First()].MainTransaction;
                foreach (var conflict in tx.GetAttributes<ConflictAttribute>())
                {
                    if (conflict.Hash == fallbackTx.Hash && tx.Signers.Any(p => p.Account == fallbackTx.Signers[1].Account)) return false;
                    if (conflict.Hash == mainTx.Hash && tx.Signers.Any(p => p.Account == mainTx.Sender)) return false;
                }
            }
            return true;
        }

        private NotaryRequest RemoveOverCapacity()
        {
            var r = sortedRequests.First();
            if (r is not null)
            {
                requests.Remove(r.FallbackTransaction.Hash);
                tasks[r.MainTransaction.Hash].Remove(r.FallbackTransaction.Hash);
                context.RemoveFallback(r.FallbackTransaction);
                sortedRequests.Remove(r);
            }
            return r;
        }

        public List<NotaryRequest> ReVerify(Transaction[] txs)
        {
            List<NotaryRequest> unverified, removed = new();
            lock (requests)
            {
                foreach (var tx in txs)
                {
                    if (tasks.TryGetValue(tx.Hash, out var fallbacks))
                    {
                        foreach (var fb in fallbacks)
                        {
                            removed.Add(requests[fb]);
                            requests.Remove(fb);
                        }
                        tasks.Remove(tx.Hash);
                    }
                    else if (requests.ContainsKey(tx.Hash))
                    {
                        tasks[requests[tx.Hash].MainTransaction.Hash].Remove(tx.Hash);
                        removed.Add(requests[tx.Hash]);
                        requests.Remove(tx.Hash);
                    }
                }
                unverified = requests.Values.ToList();
                requests.Clear();
                tasks.Clear();
                sortedRequests.Clear();
                context = new FallbackVerificationContext();
            }
            foreach (var r in unverified)
            {
                if (!TryAdd(r, out _))
                    removed.Add(r);
            }
            return removed;
        }
    }
}
