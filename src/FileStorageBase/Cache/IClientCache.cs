using System;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.Network;
using System.Collections.Generic;

namespace Neo.FileStorage.Cache
{
    public interface IFSClientCache : IDisposable
    {
        IFSClient Get(List<Address> address);
    }
}
