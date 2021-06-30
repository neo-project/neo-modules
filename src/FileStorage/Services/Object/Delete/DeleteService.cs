using System;
using System.Threading;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Object.Delete.Execute;
using Neo.FileStorage.Services.Object.Delete.Writer;
using Neo.FileStorage.Services.Object.Get;
using Neo.FileStorage.Services.Object.Put;
using Neo.FileStorage.Services.Object.Search;
using Neo.FileStorage.Services.Object.Util;

namespace Neo.FileStorage.Services.Object.Delete
{
    public class DeleteService
    {
        public Client MorphClient { get; init; }
        public PutService PutService { get; init; }
        public SearchService SearchService { get; init; }
        public GetService GetService { get; init; }
        public KeyStorage KeyStorage { get; init; }

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
