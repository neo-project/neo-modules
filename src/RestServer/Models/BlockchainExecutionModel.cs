using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;

namespace Neo.Plugins.RestServer.Models
{
    public class BlockchainExecutionModel
    {
        public TriggerType Trigger { get; set; } = TriggerType.All;
        public VMState VmState { get; set; } = VMState.NONE;
        public string Exception { get; set; } = string.Empty;
        public long GasConsumed { get; set; } = 0L;
        public StackItem[] Stack { get; set; } = System.Array.Empty<StackItem>();
        public BlockchainEventModel[] Notifications { get; set; } = System.Array.Empty<BlockchainEventModel>();
    }
}
