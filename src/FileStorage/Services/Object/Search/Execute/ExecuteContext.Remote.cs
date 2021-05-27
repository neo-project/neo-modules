namespace Neo.FileStorage.Services.Object.Search.Execute
{
    public partial class ExecuteContext
    {
        private void ProcessNode(Network.Address address)
        {
            var iport = address.ToIPAddressString();
            var client = SearchService.ClientCache.Get(iport);
            var ids = client.SearchObjects(this);
            Prm.Writer.WriteIDs(ids);
        }
    }
}
