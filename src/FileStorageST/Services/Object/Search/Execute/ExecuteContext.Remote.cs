using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Search.Execute
{
    public partial class ExecuteContext
    {
        private void ProcessNode(List<Network.Address> addresses)
        {
            var client = SearchService.ClientCache.Get(addresses);
            var ids = client.SearchObjects(this);
            Prm.Writer.WriteIDs(ids);
        }
    }
}
