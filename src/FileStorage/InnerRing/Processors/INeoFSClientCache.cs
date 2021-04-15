using Neo.FileStorage.API.Client;

namespace Neo.FileStorage.InnerRing.Processors
{
    public interface INeoFSClientCache
    {
        Client Get(string address);
    }
}
