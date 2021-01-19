using Neo.FSNode.LocalObjectStorage.LocalStore;
using NeoFS.API.v2.Client;

namespace Neo.Plugins.Innerring.Processors
{
    public interface INeoFSClientCache
    {
        Client Get(string address, params Option[] opts);
    }
}
