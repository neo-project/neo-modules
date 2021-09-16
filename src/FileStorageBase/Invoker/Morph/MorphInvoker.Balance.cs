namespace Neo.FileStorage.Invoker.Morph
{
    public partial class MorphInvoker
    {
        private const string BalanceOfMethod = "balanceOf";
        private const string DecimalsMethod = "decimals";
        private const string TransferXMethod = "transferX";
        private const string LockMethod = "lock";
        private const string MintMethod = "mint";
        private const string BurnMethod = "burn";

        public long BalanceOf(byte[] holder)
        {
            InvokeResult result = TestInvoke(BalanceContractHash, BalanceOfMethod, holder);
            return (long)result.ResultStack[0].GetInteger();
        }

        public uint BalanceDecimals()
        {
            InvokeResult result = TestInvoke(BalanceContractHash, DecimalsMethod);
            return (uint)result.ResultStack[0].GetInteger();
        }

        public void TransferX(byte[] from, byte[] to, long amount, byte[] details)
        {
            Invoke(BalanceContractHash, TransferXMethod, SideChainFee, from, to, amount, details);
        }

        public void Mint(byte[] scriptHash, long amount, byte[] comment)
        {
            Invoke(BalanceContractHash, MintMethod, SideChainFee, scriptHash, amount, comment);
        }

        public void Burn(byte[] scriptHash, long amount, byte[] comment)
        {
            Invoke(BalanceContractHash, BurnMethod, SideChainFee, scriptHash, amount, comment);
        }

        public void LockAsset(byte[] ID, UInt160 userAccount, UInt160 lockAccount, long amount, ulong until)
        {
            Invoke(BalanceContractHash, LockMethod, SideChainFee, ID, userAccount, lockAccount, amount, (int)until);
        }
    }
}
