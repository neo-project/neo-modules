using Akka.Actor;
using Google.Protobuf;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.Storage.Placement;
using Neo.FileStorage.Utils;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Neo.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public sealed class DistributeTarget : IObjectTarget
    {
        public CancellationToken Cancellation { get; init; }
        public ILocalInfoSource LocalInfo { get; init; }
        public IObjectValidator ObjectValidator { get; init; }
        public IActorRef RemotePool { get; init; }
        public IActorRef LocalPool { get; init; }
        public Func<ITraverser> TraverserInitializer { get; init; }
        public Func<Node, CancellationToken, IObjectTarget> NodeTargetInitializer { get; init; }
        public Func<Node, bool> Relay { get; init; }

        private FSObject obj;
        private byte[] payload = Array.Empty<byte>();
        private string lastError = "incomplete object PUT by placement";

        public void WriteHeader(FSObject init)
        {
            obj = init;
        }

        public void WriteChunk(byte[] chunk)
        {
            payload = Concat(payload, chunk);
        }

        public AccessIdentifiers Close()
        {
            obj.Payload = ByteString.CopyFrom(payload);
            payload = Array.Empty<byte>();
            if (!ObjectValidator.ValidateContent(obj))
                throw new InvalidOperationException($"{nameof(DistributeTarget)} invalid content");
            var traverser = TraverserInitializer();
            while (true)
            {
                if (Cancellation.IsCancellationRequested) throw new TaskCanceledException();
                var ns = traverser.Next();
                if (ns.Count == 0) break;
                var tasks = new Task[ns.Count];
                for (int i = 0; i < ns.Count; i++)
                {
                    var n = ns[i];
                    var isLocal = LocalInfo.PublicKey.SequenceEqual(n.PublicKey);
                    tasks[i] = new(() =>
                    {
                        try
                        {
                            if (Relay is not null && !isLocal)
                            {
                                Relay(n);
                                traverser.SubmitSuccess();
                                return;
                            }
                            var target = NodeTargetInitializer(n, Cancellation);
                            target.WriteHeader(obj);
                            target.Close();
                            traverser.SubmitSuccess();
                        }
                        catch (Exception e)
                        {
                            string error = $"could not distribute object, node={n.Addresses[0]}, error={e.Message}";
                            Utility.Log(nameof(DistributeTarget), LogLevel.Warning, error);
                            lastError = error;
                            return;
                        }
                    }, Cancellation);
                    if (isLocal)
                        LocalPool.Tell(new WorkerPool.NewTask { Process = obj.ObjectId.String(), Task = tasks[i] });
                    else
                        RemotePool.Tell(new WorkerPool.NewTask { Process = obj.ObjectId.String(), Task = tasks[i] });
                }
                Task.WaitAll(tasks);
            }
            if (!traverser.Success())
                throw new InvalidOperationException($"incomplete object put, error={lastError}");
            return new AccessIdentifiers
            {
                Self = obj.ObjectId,
            };
        }

        public void Dispose() { }
    }
}
