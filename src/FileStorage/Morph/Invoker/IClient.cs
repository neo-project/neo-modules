using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;

namespace Neo.FileStorage.Morph.Invoker
{
    public interface IClient
    {
        public Wallet GetWallet();
        public bool Invoke(out UInt256 txId, UInt160 contractHash, string method, long fee, params object[] args);
        public InvokeResult TestInvoke(UInt160 contractHash, string method, params object[] args);
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
