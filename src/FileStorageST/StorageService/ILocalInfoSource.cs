using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Network;
using Neo.FileStorage.Storage.Services.Control.Service;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage
{
    public interface ILocalInfoSource
    {
        List<Address> Addresses { get; }
        API.Netmap.NodeInfo NodeInfo { get; }
        uint Network { get; }
        HealthStatus HealthStatus { get; }
    }
}
