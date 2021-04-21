using System.Threading;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.Reputaion.Local.Client;

namespace Neo.FileStorage.Services.Object.Put
{
    public class RemoteSender
    {
        public KeyStorage KeyStorage { get; init; }
        public ReputaionClientCache ClientCache { get; init; }

        public void PutObject(RemotePutPrm prm, CancellationToken context)
        {
            var t = new RemoteTarget
            {
                KeyStorage = KeyStorage,
                Address = prm.Node,
                ClientCache = ClientCache,
            };
            t.WriteHeader(prm.Object);
            t.Close();
        }
    }
}
