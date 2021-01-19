using Neo.VM;
using Neo.VM.Types;

namespace Neo.Plugins.FSStorage.morph.invoke
{
    public interface IClient
    {
        public bool InvokeFunction(UInt160 contractHash, string method, long fee, params object[] args);
        public InvokeResult InvokeLocalFunction(UInt160 contractHash, string method, params object[] args);
    }

    public class InvokeResult
    {
        public VMState State;
        public long GasConsumed;
        public byte[] Script;
        public StackItem[] ResultStack;

        public override string ToString()
        {
            return string.Format("InvokeResult:VMState:{0}:GasConsumed:{1}:Script:{2}:ResultStack:{3}", State,GasConsumed,Script.ToHexString(),ResultStack.Length);
        }
    }
}
