using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;

namespace Neo.Plugins
{
    class ApplicationDebugger : ApplicationEngine
    {
        public ApplicationDebugger(TriggerType trigger, IVerifiable container, DataCache snapshot, Block persistingBlock, ProtocolSettings settings, long gas, Diagnostic diagnostic) : base(trigger, container, snapshot, persistingBlock, settings, gas, diagnostic)
        {
        }

        public void SetState(VMState state)
        {
            State = state;
        }

        public new void ExecuteNext()
        {
            base.ExecuteNext();
        }
    }
}
