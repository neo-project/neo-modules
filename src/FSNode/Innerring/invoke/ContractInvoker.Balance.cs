using Neo.Plugins.FSStorage.morph.invoke;

namespace Neo.Plugins.FSStorage.innerring.invoke
{
    public partial class ContractInvoker
    {
        private static UInt160 BalanceContractHash => Settings.Default.BalanceContractHash;
        private const string TransferXMethod = "transferX";
        private const string LockMethod = "lock";
        private const string MintMethod = "mint";
        private const string BurnMethod = "burn";
        private const string PrecisionMethod = "decimals";

        private const long ExtraFee = 1_5000_0000;

        public static bool TransferBalanceX(Client client, byte[] sender, byte[] receiver, long amount, byte[] comment)
        {
            return client.Invoke(out _,BalanceContractHash, TransferXMethod, ExtraFee, sender, receiver, amount, comment);
        }

        public static bool Mint(Client client, byte[] scriptHash, long amount, byte[] comment)
        {
            return client.Invoke(out _, BalanceContractHash, MintMethod, ExtraFee, scriptHash, amount, comment);
        }

        public static bool Burn(Client client, byte[] scriptHash, long amount, byte[] comment)
        {
            return client.Invoke(out _, BalanceContractHash, BurnMethod, ExtraFee, scriptHash, amount, comment);
        }

        public static bool LockAsset(Client client, byte[] ID, UInt160 userAccount, UInt160 lockAccount, long amount, ulong until)
        {
            return client.Invoke(out _, BalanceContractHash, LockMethod, ExtraFee, ID, userAccount, lockAccount, amount, (int)until);
        }

        public static uint BalancePrecision(Client client)
        {
            InvokeResult result = client.TestInvoke(BalanceContractHash, PrecisionMethod);
            return (uint)result.ResultStack[0].GetInteger();
        }
    }
}
