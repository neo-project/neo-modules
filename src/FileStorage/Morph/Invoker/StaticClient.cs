namespace Neo.FileStorage.Morph.Invoker
{
    public class StaticClient
    {
        public Client Client;
        public UInt160 ContractHash;
        public long Fee;

        public bool InvokeFunction(string method, object[] args = null)
        {
            return Client.Invoke(out _, ContractHash, method, Fee, args);
        }

        public InvokeResult InvokeLocalFunction(string method, object[] args = null)
        {
            return Client.TestInvoke(ContractHash, method, args);
        }
    }
}
