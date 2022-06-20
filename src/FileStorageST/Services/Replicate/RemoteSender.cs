using Neo.FileStorage.Storage.Services.Object.Util;
using Neo.FileStorage.Storage.Services.Object.Put.Target;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Replicate
{
    public class RemoteSender : IRemoteSender
    {
        public KeyStore KeyStorage { get; init; }
        public IPutClientCache ClientCache { get; init; }

        public void PutObject(RemotePutPrm prm, CancellationToken cancellation)
        {
            var t = new RemoteTarget
            {
                Cancellation = cancellation,
                KeyStorage = KeyStorage,
                Node = prm.Node,
                PutClientCache = ClientCache,
            };
            t.WriteHeader(prm.Object);
            t.Close();
        }
    }
}
