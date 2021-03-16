using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.Object.Delete.Execute;
using Neo.FileStorage.Services.Object.Delete.Writer;
using Neo.FileStorage.Services.Object.Get;
using Neo.FileStorage.Services.Object.Put;
using Neo.FileStorage.Services.Object.Search;
using Neo.FileStorage.Services.Object.Util;
using System;

namespace Neo.FileStorage.Services.Object.Delete
{
    public class DeleteService
    {
        private readonly OwnerID selfId;
        public readonly PutService PutService;
        public readonly SearchService SearchService;
        public readonly GetService GetService;
        private readonly KeyStorage keyStorage;

        public Address Delete(DeletePrm prm)
        {
            var writer = new SimpleTombstoneWriter();
            var executor = new Execute.Executor
            {
                Context = new ExecuteContext
                {
                    DeleteService = this,
                    Prm = prm,
                }
            };
            executor.Execute();
            return writer.Address;
        }

        public DeletePrm ToDeletePrm(DeleteRequest request)
        {
            var meta = request.MetaHeader;
            var key = keyStorage.GetKey(meta.SessionToken);
            if (key is null) throw new InvalidOperationException(nameof(DeleteService) + " could not get key");
            var prm = DeletePrm.FromRequest(request);
            prm.Key = key;
            return prm;
        }
    }
}