using Neo.SmartContract;
using Neo.VM;

namespace Neo.Plugins.RestServer.Models
{
    public class ExecutionEngineModel
    {
        public long GasConsumed { get; set; }
        public VMState State { get; set; }
        public IReadOnlyList<NotifyEventArgs> Notifications { get; set; }
        public EvaluationStack ResultStack { get; set; }
        public Exception FaultException { get; set; }
    }
}
