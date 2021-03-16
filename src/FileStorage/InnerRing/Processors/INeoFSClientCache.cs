using Neo.FileStorage.LocalObjectStorage.LocalStore;
using Neo.FileStorage.API.Client;

namespace Neo.Plugins.Innerring.Processors
{
    public interface INeoFSClientCache
    {
        Client Get(string address, params Option[] opts);
    }
}
