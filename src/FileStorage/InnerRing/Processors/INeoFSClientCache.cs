using Neo.FileStorage.API.Client;

namespace Neo.Plugins.Innerring.Processors
{
    public interface INeoFSClientCache
    {
        Client Get(string address);
    }
}
