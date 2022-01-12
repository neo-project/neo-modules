using System;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;

namespace Neo.FileStorage.Cache
{
    public interface IFSClientCache : IDisposable
    {
        IFSClient Get(NodeInfo node);
    }
}
