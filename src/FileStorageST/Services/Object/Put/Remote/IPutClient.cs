using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System;
using System.Threading;
using System.Threading.Tasks;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Put.Remote
{
    public interface IPutClient
    {
        Task<ObjectID> PutObject(FSObject obj, CallOptions options = null, CancellationToken context = default);
        Task<IClientStream> PutObject(PutRequest init, DateTime? deadline = null, CancellationToken context = default);
    }
}
