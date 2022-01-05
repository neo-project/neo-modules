using Neo.FileStorage.API.Control;

namespace Neo.FileStorage.Storage
{
    public interface ILocalInfoSource
    {
        byte[] PublicKey { get; }
        API.Netmap.NodeInfo NodeInfo { get; }
        uint Network { get; }
        HealthStatus HealthStatus { get; }
        ProtocolSettings ProtocolSettings { get; }
    }
}
