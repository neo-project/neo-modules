using System.Threading;

namespace Neo.FileStorage.Storage.Services.Replicate
{
    public interface IRemoteSender
    {
        void PutObject(RemotePutPrm prm, CancellationToken context);
    }
}
