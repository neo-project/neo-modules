using Neo.FileStorage.API.Object;
using Neo.FileStorage.Storage.Services.Object.Delete.Execute;
using Neo.FileStorage.Storage.Services.Object.Delete.Writer;
using Neo.FileStorage.Storage.Services.Object.Get;
using Neo.FileStorage.Storage.Services.Object.Put;
using Neo.FileStorage.Storage.Services.Object.Search;
using Neo.FileStorage.Storage.Services.Object.Util;
using System;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Object.Delete
{
    public class DeleteService
    {
        public const ulong DefaultTomestoneLifetime = 5;
        public IEpochSource EpochSource { get; init; }
        public PutService PutService { get; init; }
        public SearchService SearchService { get; init; }
        public GetService GetService { get; init; }
        public KeyStore KeyStorage { get; init; }
        public ILocalInfoSource LocalInfo { get; init; }
        public ulong TombstoneLifetime { get; init; } = DefaultTomestoneLifetime;

        public DeletePrm ToDeletePrm(DeleteRequest request, DeleteResponse response)
        {
            var meta = request.MetaHeader;
            var key = KeyStorage.GetKey(meta?.SessionToken);
            if (key is null) throw new InvalidOperationException(nameof(ToDeletePrm) + " could not get key");
            var prm = DeletePrm.FromRequest(request);
            prm.Key = key;
            prm.Writer = new SimpleTombstoneWriter
            {
                Response = response,
            };
            return prm;
        }

        public void Delete(DeletePrm prm, CancellationToken cancellation)
        {
            var executor = new ExecuteContext
            {
                Cancellation = cancellation,
                DeleteService = this,
                Prm = prm,
            };
            executor.Execute();
        }
    }
}
