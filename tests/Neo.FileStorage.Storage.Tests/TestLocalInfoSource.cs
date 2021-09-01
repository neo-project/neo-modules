using Neo.FileStorage.Storage.Services.Control.Service;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Tests
{
    public class TestLocalInfo : ILocalInfoSource
    {
        public List<Network.Address> Addresses { get; set; }
        public API.Netmap.NodeInfo NodeInfo { get; set; }
        public uint Network { get; set; }
        public HealthStatus HealthStatus { get; set; }
    }
}
