using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using Neo.FSNode.Services.Object.Delete.Writer;
using Neo.FSNode.Services.Object.Put;
using Neo.FSNode.Services.Object.Util;
using System;
using Neo.FSNode.Services.Object.Search;
using Neo.FSNode.Services.Object.Get;

namespace Neo.FSNode.Services.Object.Delete
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
            var executor = new Executor
            {
                DeleteService = this,
                Prm = prm,
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