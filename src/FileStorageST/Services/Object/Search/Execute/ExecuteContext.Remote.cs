using System;
using Neo.FileStorage.API.Netmap;

namespace Neo.FileStorage.Storage.Services.Object.Search.Execute
{
    public partial class ExecuteContext
    {
        private void ProcessNode(NodeInfo node)
        {
            var client = SearchService.ClientCache.Get(node);
            try
            {
                var ids = client.SearchObjects(this);
                Prm.Writer.WriteIDs(ids);
            }
            catch (Exception e)
            {
                Utility.Log(nameof(Search.Execute), LogLevel.Warning, e.Message);
            }
        }
    }
}
