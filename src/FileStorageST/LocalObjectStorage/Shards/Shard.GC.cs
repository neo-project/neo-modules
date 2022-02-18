using Akka.Actor;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public partial class Shard : IDisposable
    {
        public const int DefaultRemoveBatchSize = 100;
        public const int DefaultRemoveInterval = 60000;
        private readonly int removeBatchSize;
        private readonly int removeInteral;
        private Timer timer;
        private readonly Action<List<FSAddress>, CancellationToken> expiredTomestonesCallback;
        private CancellationTokenSource cancellationSource;
        private Task prevGroup;

        public void HandleExpiredTombstones(HashSet<FSAddress> tss)
        {
            List<FSAddress> inhume = new();
            metabase.IterateCoveredByTombstones(tss, address =>
            {
                inhume.Add(address);
            });
            metabase.Inhume(null, tss.ToArray());
            if (inhume.Count == 0) return;
            metabase.Inhume(null, inhume.ToArray());
        }

        private void CollectExpiredObjects(ulong epoch, CancellationToken cancellation)
        {
            List<FSAddress> expired = new();
            try
            {
                metabase.IterateExpired(epoch, (t, address) =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (t != ObjectType.Tombstone)
                        expired.Add(address);
                });
            }
            catch (Exception e)
            {
                Utility.Log(nameof(Shard), LogLevel.Warning, $"iterator over expired objects failed, error={e.Message}");
                return;
            }
            if (expired.Count == 0) return;
            if (cancellation.IsCancellationRequested) return;
            Inhume(null, expired.ToArray());
        }

        private void CollectExpiredTombstones(ulong epoch, CancellationToken cancellation)
        {
            List<FSAddress> expired = new();
            try
            {
                metabase.IterateExpired(epoch, (t, address) =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (t == ObjectType.Tombstone)
                        expired.Add(address);
                });
            }
            catch (Exception e)
            {
                Utility.Log(nameof(Shard), LogLevel.Warning, $"iterator over expired objects failed, error={e.Message}");
                return;
            }
            if (expired.Count == 0) return;
            if (cancellation.IsCancellationRequested) return;
            if (expiredTomestonesCallback is not null) expiredTomestonesCallback(expired, cancellation);
        }

        public void OnNewEpoch(ulong epoch)
        {
            cancellationSource?.Cancel();
            prevGroup?.Wait();
            cancellationSource?.Dispose();
            cancellationSource = new();
            Task t1 = new(() =>
            {
                CollectExpiredObjects(epoch, cancellationSource.Token);
            }, cancellationSource.Token);
            Task t2 = new(() =>
            {
                CollectExpiredTombstones(epoch, cancellationSource.Token);
            }, cancellationSource.Token);
            workPool.Tell(new WorkerPool.NewTask
            {
                Process = "Shard.CollectExpiredObjects",
                Task = t1,
            });
            workPool.Tell(new WorkerPool.NewTask
            {
                Process = "Shard.CollectExpiredTombstones",
                Task = t2,
            });
            prevGroup = Task.WhenAll(t1, t2);
        }

        private void RemoveGarbage(object state)
        {
            List<FSAddress> buf = new();
            metabase.IterateGraveYard(g =>
            {
                if (g.GCMark)
                    buf.Add(g.Address);
                return removeBatchSize <= buf.Count;
            });
            if (buf.Count == 0) return;
            try
            {
                Delete(buf.ToArray());
            }
            catch (Exception e)
            {
                Utility.Log(nameof(Shard), LogLevel.Warning, $"can't delete objects, error={e.Message}");
            }
        }
    }
}
