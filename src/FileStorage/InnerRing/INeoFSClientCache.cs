using System;
using Neo.FileStorage.API.Client;

namespace Neo.FileStorage.InnerRing
{
    public interface INeoFSClientCache : IDisposable
    {
        Client Get(Network.Address address);
    }
}
