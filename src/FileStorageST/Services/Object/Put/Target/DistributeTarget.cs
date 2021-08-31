using Google.Protobuf;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.Storage.Placement;
using Neo.FileStorage.Storage.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Neo.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public sealed class DistributeTarget : IObjectTarget
    {
        public ITraverser Traverser { get; init; }
        public IObjectValidator ObjectValidator { get; init; }
        public Func<List<Network.Address>, IObjectTarget> NodeTargetInitializer { get; init; }
        public Func<List<Network.Address>, bool> Relay;

        private FSObject obj;
        private byte[] payload = Array.Empty<byte>();

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
            obj.Payload = obj.Payload.Concat(ByteString.CopyFrom(payload));
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
                        try
                        {
                            if (Relay is null || !Relay(addrs))
                            {
                                var target = NodeTargetInitializer(addrs);
                                target.WriteHeader(obj);
                                target.Close();
                            }
                        }
                        catch (Exception e)
                        {
                            Utility.Log(nameof(DistributeTarget), LogLevel.Debug, e.Message);
                            return;
                        }
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
