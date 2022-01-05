using Neo.FileStorage.API.Cryptography.Tz;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public class HomomorphicHasher : IDisposable
    {
        private readonly int MaxRunningTasks;
        private readonly int MaxWaitingTasks;
        private readonly CancellationToken cancellation;
        private byte[] prev;
        private readonly Task deamon;
        private readonly List<Task> tasks = new();
        private readonly ConcurrentDictionary<int, byte[]> hashes = new();
        private readonly ConcurrentQueue<Task> queue = new();
        private bool disposed = false;
        private int index = -1;
        public byte[] Hash
        {
            get
            {
                while (hashes.Count < index + 1 && !cancellation.IsCancellationRequested && !disposed)
                {
                    Thread.Sleep(100);
                }
                List<byte[]> hs = hashes.OrderBy(p => p.Key).Select(p => p.Value).ToList();
                if (prev is not null)
                {
                    if (hashes.IsEmpty) return prev;
                    hs.Insert(0, prev);
                }
                var hash = TzHash.Concat(hs);
                hashes.Clear();
                index = -1;
                return prev = hash;
            }
        }

        public HomomorphicHasher(CancellationToken cancellation = default)
        {
            MaxRunningTasks = Environment.ProcessorCount;
            MaxWaitingTasks = MaxRunningTasks;
            this.cancellation = cancellation;
            deamon = Task.Run(() => Deamon(), cancellation);
        }

        public void Dispose()
        {
            disposed = true;
            deamon.Wait();
        }

        public void WriteChunk(byte[] chunk)
        {
            var i = ++index;
            queue.Enqueue(new Task(() =>
            {
                Calculatehash(i, chunk);
            }));
            if (MaxWaitingTasks < queue.Count)
            {
                while (MaxWaitingTasks / 2 < queue.Count && !disposed && !cancellation.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void Deamon()
        {
            while (!disposed && !cancellation.IsCancellationRequested)
            {
                while (tasks.Count < MaxRunningTasks && queue.TryDequeue(out var task))
                {
                    tasks.Add(task);
                    task.Start();
                }
                List<Task> to_del = new();
                foreach (var t in tasks)
                {
                    if (t.Status != TaskStatus.Running && t.Status != TaskStatus.WaitingToRun)
                    {
                        to_del.Add(t);
                    }
                }
                foreach (var t in to_del)
                    tasks.Remove(t);
            }
        }

        public void Calculatehash(int index, byte[] chunk)
        {
            using var tz = new TzHash();
            var hash = tz.ComputeHash(chunk);
            lock (hashes)
            {
                hashes[index] = hash;
            }
        }
    }
}
