using Neo.Network.P2P.Payloads;

namespace Neo.Plugins.RestServer.Models
{
    public class TransactionAttributeModel
    {
        public bool AllowMultiple { get; set; }
        public TransactionAttributeType Type { get; set; }
    }
}
