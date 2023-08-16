using Neo.VM.Types;
using Array = System.Array;

namespace Neo.Plugins.RestServer.Models
{
    public class BlockchainEventModel
    {
        public UInt160 ScriptHash { get; set; } = new();
        public string EventName { get; set; } = string.Empty;
        public StackItem[] State { get; set; } = Array.Empty<StackItem>();
    }
}
