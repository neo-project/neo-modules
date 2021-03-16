using Neo.VM;
using Neo.VM.Types;

namespace Neo.FileStorage.Morph.Invoke
{
    public interface IClient
    {
        bool InvokeFunction(UInt160 contractHash, string method, long fee, params object[] args);
        InvokeResult InvokeLocalFunction(UInt160 contractHash, string method, params object[] args);
    }

    public class InvokeResult
    {
        public VMState State;
        public long GasConsumed;
        public byte[] Script;
        public StackItem[] ResultStack;

        public override string ToString()
        {
            return string.Format("InvokeResult:VMState:{0}:GasConsumed:{1}:Script:{2}:ResultStack:{3}", State, GasConsumed, Script.ToHexString(), ResultStack.Length);
        }
    }
}
