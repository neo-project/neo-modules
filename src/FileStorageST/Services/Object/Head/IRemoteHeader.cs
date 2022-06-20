using System.Threading;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Head
{
    public interface IRemoteHeader
    {
        FSObject Head(RemoteHeadPrm prm, CancellationToken cancellation);
    }
}
