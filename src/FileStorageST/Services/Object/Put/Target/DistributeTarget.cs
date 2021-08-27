using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.Storage.Placement;
using FSObject = Neo.FileStorage.API.Object.Object;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public sealed class DistributeTarget : IObjectTarget
    {
        public ITraverser Traverser { get; init; }
        public IObjectValidator ObjectValidator { get; init; }
        public Func<List<Network.Address>, IObjectTarget> NodeTargetInitializer { get; init; }
        public Action<List<Network.Address>> Relay;

        private FSObject obj;
        private byte[] payload;
        private int offset;

        public void WriteHeader(FSObject init)
        {
            obj = init;
            payload = new byte[obj.PayloadSize];
            offset = 0;
        }

        public void WriteChunk(byte[] chunk)
        {
            chunk.CopyTo(payload, offset);
            offset += chunk.Length;
        }

        public AccessIdentifiers Close()
        {
            obj.Payload = ByteString.CopyFrom(payload);
            if (!ObjectValidator.ValidateContent(obj))
                throw new InvalidOperationException($"{nameof(DistributeTarget)} invalid content");
            while (true)
            {
                var addrss = Traverser.Next();
                if (!addrss.Any()) break;
                var tasks = new Task[addrss.Count];
                for (int i = 0; i < addrss.Count; i++)
                {
                    var addrs = addrss[i];
                    tasks[i] = Task.Run(() =>
                    {
                        if (Relay is not null)
                            Relay(addrs);
                        var target = NodeTargetInitializer(addrs);
                        if (target is null) return;
                        target.WriteHeader(obj);
                        target.Close();
                        Traverser.SubmitSuccess();
                    });
                }
                Task.WaitAll(tasks);
            }
            if (!Traverser.Success())
                throw new InvalidOperationException($"{nameof(DistributeTarget)} incomplete object put");
            return new AccessIdentifiers
            {
                Self = obj.ObjectId,
            };
        }

        public void Dispose() { }
    }
}
