namespace Neo.FileStorage.Services.Object.Search.Remote
{
    public interface ISearchClientCache
    {
        ISearchClient Get(Network.Address address);
    }
}
