using Neo.FileStorage.API.Object;
using Neo.FileStorage.Services.Object.Delete;
using Neo.FileStorage.Services.Object.Get;
using Neo.FileStorage.Services.Object.Put;
using Neo.FileStorage.Services.Object.Search;
using System;
using System.Threading;

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

        public void GetRange(GetRangeRequest request, Action<GetRangeResponse> handler)
        {
            var prm = GetService.ToRangePrm(request, handler);
            GetService.GetRange(prm);
        }

        public GetRangeHashResponse GetRangeHash(GetRangeHashRequest request)
        {
            var resp = new GetRangeHashResponse();
            var prm = GetService.ToRangeHashPrm(request, resp);
            GetService.GetRangeHash(prm);
            return resp;
        }

        public HeadResponse Head(HeadRequest request)
        {
            var resp = new HeadResponse();
            var prm = GetService.ToHeadPrm(request, resp);
            GetService.Head(prm);
            //TODO: split exception
            return resp;
        }

        public PutStream Put(CancellationToken cancellation)
        {
            return PutService.Put(cancellation);
        }

        public void Search(SearchRequest request, Action<SearchResponse> handler)
        {
            var prm = SearchService.ToSearchPrm(request, handler);
            SearchService.Search(prm);
        }
    }
}
