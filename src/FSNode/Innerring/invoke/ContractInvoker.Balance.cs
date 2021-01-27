using Neo.Plugins.FSStorage.morph.invoke;
using System;

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

        public static bool TransferBalanceX(IClient client, byte[] sender, byte[] receiver, long amount, byte[] comment)
        {
            return client.InvokeFunction(BalanceContractHash, TransferXMethod, ExtraFee, sender, receiver, amount, comment);
        }

        public static bool Mint(IClient client, byte[] scriptHash, long amount, byte[] comment)
        {
            return client.InvokeFunction(BalanceContractHash, MintMethod, ExtraFee, scriptHash, amount, comment);
        }

        public static bool Burn(IClient client, byte[] scriptHash, long amount, byte[] comment)
        {
            return client.InvokeFunction(BalanceContractHash, BurnMethod, ExtraFee, scriptHash, amount, comment);
        }

        public static bool LockAsset(IClient client, byte[] ID, UInt160 userAccount, UInt160 lockAccount, long amount, ulong until)
        {
            return client.InvokeFunction(BalanceContractHash, LockMethod, ExtraFee, ID, userAccount, lockAccount, amount, (int)until);
        }

        public static uint BalancePrecision(IClient client)
        {
            InvokeResult result = client.InvokeLocalFunction(BalanceContractHash, PrecisionMethod);
            return (uint)result.ResultStack[0].GetInteger();
        }
    }
}
