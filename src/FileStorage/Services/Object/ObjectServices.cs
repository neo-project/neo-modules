using Neo.FileStorage.API.Object;
using Neo.FileStorage.Services.Object.Delete;
using Neo.FileStorage.Services.Object.Get;
using Neo.FileStorage.Services.Object.Put;
using Neo.FileStorage.Services.Object.Search;
using System;

namespace Neo.FileStorage.Services.Object
{
    public class ObjectServices
    {
        public GetService GetService { get; init; }
        public PutService PutService { get; init; }
        public DeleteService DeleteService { get; init; }
        public SearchService SearchService { get; init; }

        public DeleteResponse Delete(DeleteRequest request)
        {
            var resp = new DeleteResponse();
            var prm = DeleteService.ToDeletePrm(request, resp);
            DeleteService.Delete(prm);
            return resp;
        }

        public void Get(GetRequest request, Action<GetResponse> handler)
        {
            var prm = GetService.ToGetPrm(request, handler);
            GetService.Get(prm);
            //TODO: split exception
        }

        public HeadResponse Head(HeadRequest request)
        {
            var resp = new HeadResponse();
            var prm = GetService.ToHeadPrm(request, resp);
            GetService.Head(prm);
            //TODO: split exception
            return resp;
        }

        public PutStream Put()
        {
            return PutService.Put();
        }
    }
}
