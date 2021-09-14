using Neo.VM;
using Neo.VM.Types;
using System;

namespace Neo.FileStorage.Invoker
{
    public class InvokeResult
    {
        public VMState State;
        public long GasConsumed;
        public byte[] Script;
        public Exception FaultException;
        public StackItem UncaughtException;
        public StackItem[] ResultStack;

        public override string ToString()
        {
            return string.Format("InvokeResult:VMState:{0}:GasConsumed:{1}:Script:{2}:ResultStack:{3}", State, GasConsumed, Script.ToHexString(), ResultStack.Length);
        }
    }
}
