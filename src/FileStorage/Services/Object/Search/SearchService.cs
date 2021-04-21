using Neo.FileStorage.API.Object;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Object.Search.Execute;
using Neo.FileStorage.Services.Object.Search.Writer;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.Reputaion.Local.Client;
using System;

namespace Neo.FileStorage.Services.Object.Search
{
    public class SearchService
    {
        public KeyStorage KeyStorage { get; init; }
        public StorageEngine LocalStorage { get; init; }
        public Client MorphClient { get; init; }
        public ReputaionClientCache ClientCache { get; init; }
        public TraverserGenerator TraverserGenerator { get; init; }

        public SearchPrm ToSearchPrm(SearchRequest request, Action<SearchResponse> handler)
        {
            var key = KeyStorage.GetKey(request.MetaHeader.SessionToken);
            var prm = SearchPrm.FromRequest(request);
            prm.Key = key;
            prm.Writer = new SearchStream
            {
                Handler = handler,
            };
            return prm;
        }

        public void Search(SearchPrm prm)
        {
            ExecuteContext executor = new()
            {
                Prm = prm,
                SearchService = this,
            };
            executor.Execute();
        }
    }
}
