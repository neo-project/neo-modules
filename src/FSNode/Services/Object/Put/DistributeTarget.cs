using Google.Protobuf;
using V2Address = NeoFS.API.v2.Refs.Address;
using V2Object = NeoFS.API.v2.Object.Object;
using Neo.FSNode.Network;
using Neo.FSNode.Services.Object.Put.Store;
using Neo.FSNode.Services.ObjectManager.Placement;
using Neo.FSNode.Services.ObjectManager.Transformer;
using System;
using System.Threading.Tasks;

namespace Neo.FSNode.Services.Object.Put
{
    public class DistributeTarget : ValidatingTarget
    {
        private ILocalAddressSource localAddressSource;
        public PutInitPrm Prm;
        private V2Object obj;

        public override void WriteHeader(V2Object init)
        {
            base.WriteHeader(init);
            obj = init;
        }

        public override void WriteChunk(byte[] chunk)
        {
            base.WriteChunk(chunk);
        }

        public override AccessIdentifiers Close()
        {
            base.Close();
            var traverser = new Traverser()
            {
                Address = new V2Address
                {
                    ContainerId = Prm.Container.CalCulateAndGetID,
                    ObjectId = Prm.Init.ObjectId,
                },
                Builder = Prm.Builder,
                Policy = Prm.Container.PlacementPolicy,
            };
            if (!ObjectValidator.ValidateContent(obj.Header.ObjectType, payload))
                throw new InvalidOperationException(nameof(DistributeTarget) + " invalid content");
            obj.Payload = ByteString.CopyFrom(payload);
            while (true)
            {
                var addrs = traverser.Next();
                if (addrs.Length == 0) break;
                var tasks = new Task[addrs.Length];
                for (int i = 0; i < addrs.Length; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        IStore store = null;
                        if (addrs[i].IsLocalAddress(localAddressSource))
                        {
                            store = new LocalStore();
                        }
                        else
                        {
                            store = new RemoteStore();
                        }
                        try
                        {
                            store.Put(obj);
                            traverser.SubmitSuccess();
                        }
                        catch
                        {

                        }
                    });
                }
                Task.WaitAll(tasks);
            }
            if (!traverser.Success())
                throw new InvalidOperationException(nameof(DistributeTarget) + " incomplete object put");
            return new AccessIdentifiers
            {
                Self = obj.ObjectId,
            };
        }
    }
}
