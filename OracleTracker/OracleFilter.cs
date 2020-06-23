using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;

namespace Neo.Oracle
{
    public class OracleFilter
    {
        public UInt160 ContractHash;

        public string FilterMethod;

        public string FilterArgs;

        public static bool Filter(StoreView snapshot, OracleFilter filter, byte[] input, out byte[] result, out long gasCost)
        {
            if (filter == null)
            {
                result = input;
                gasCost = 0;
                return true;
            }
            // Prepare the execution
            using ScriptBuilder script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Contract_CallEx, filter.ContractHash, filter.FilterMethod, new object[] { input, filter.FilterArgs }, (byte)CallFlags.None);

            // Execute
            using var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            engine.LoadScript(script.ToArray(), CallFlags.AllowCall);

            if (engine.Execute() != VMState.HALT || !engine.ResultStack.TryPop(out PrimitiveType ret))
            {
                result = null;
                gasCost = engine.GasConsumed;
                return false;
            }
            result = ret.GetSpan().ToArray();
            gasCost = engine.GasConsumed;
            return true;
        }
    }
}
