namespace Neo.Plugins.FSStorage.morph.invoke
{
    public class StaticClient
    {
        public IClient Client;
        public UInt160 ContractHash;
        public long Fee;

        public bool InvokeFunction(string method, object[] args = null)
        {
            return Client.InvokeFunction(ContractHash, method, Fee, args);
        }

        public InvokeResult InvokeLocalFunction(string method, object[] args = null)
        {
            return Client.InvokeLocalFunction(ContractHash, method, args);
        }
    }
}
