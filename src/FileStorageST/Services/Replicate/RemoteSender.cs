using System.Threading;
using Neo.FileStorage.Storage.Services.Object.Util;
using Neo.FileStorage.Storage.Services.Object.Put.Target;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;

namespace Neo.FileStorage.Storage.Services.Replicate
{
    public class RemoteSender : IRemoteSender
    {
        public KeyStore KeyStorage { get; init; }
        public IPutClientCache ClientCache { get; init; }

        public void PutObject(RemotePutPrm prm, CancellationToken context)
        {
            var t = new RemoteTarget
            {
                Token = context,
                KeyStorage = KeyStorage,
                Addresses = prm.Addresses,
                PutClientCache = ClientCache,
            };
            t.WriteHeader(prm.Object);
            t.Close();
        }
    }
}
