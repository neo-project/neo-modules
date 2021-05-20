using Neo.FileStorage.API.Client;

namespace Neo.FileStorage.InnerRing
{
    public interface INeoFSClientCache
    {
        Client Get(string address);
    }
}
