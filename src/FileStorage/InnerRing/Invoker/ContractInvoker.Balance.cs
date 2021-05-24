using Neo.FileStorage.Morph.Invoker;
using Neo.VM;
using System;

namespace Neo.FileStorage.InnerRing.Invoker
{
    public static partial class ContractInvoker
    {
        private static UInt160 BalanceContractHash => Settings.Default.BalanceContractHash;
        private const string TransferXMethod = "transferX";
        private const string LockMethod = "lock";
        private const string MintMethod = "mint";
        private const string BurnMethod = "burn";
        private const string PrecisionMethod = "decimals";

        public static bool Mint(this Client client, byte[] scriptHash, long amount, byte[] comment)
        {
            if (client is null) throw new Exception("client is nil");
            return client.Invoke(out _, BalanceContractHash, MintMethod, SideChainFee, scriptHash, amount, comment);
        }

        public static bool Burn(this Client client, byte[] scriptHash, long amount, byte[] comment)
        {
            if (client is null) throw new Exception("client is nil");
            return client.Invoke(out _, BalanceContractHash, BurnMethod, SideChainFee, scriptHash, amount, comment);
        }

        public static bool LockAsset(this Client client, byte[] ID, UInt160 userAccount, UInt160 lockAccount, long amount, ulong until)
        {
            if (client is null) throw new Exception("client is nil");
            return client.Invoke(out _, BalanceContractHash, LockMethod, SideChainFee, ID, userAccount, lockAccount, amount, (int)until);
        }

        public static uint BalancePrecision(this Client client)
        {
            if (client is null) throw new Exception("client is nil");
            InvokeResult result = client.TestInvoke(BalanceContractHash, PrecisionMethod);
            if (result.State != VMState.HALT) throw new Exception("can't get BalancePrecision");
            return (uint)result.ResultStack[0].GetInteger();
        }
    }
}
