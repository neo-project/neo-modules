using Neo.FileStorage.API.Control;

namespace Neo.FileStorage.Storage.Tests
{
    public class TestLocalInfo : ILocalInfoSource
    {
        public byte[] PublicKey { get; set; }
        public API.Netmap.NodeInfo NodeInfo { get; set; }
        public uint Network { get; set; }
        public HealthStatus HealthStatus { get; set; }
        public ProtocolSettings ProtocolSettings { get; set; }
    }
}
