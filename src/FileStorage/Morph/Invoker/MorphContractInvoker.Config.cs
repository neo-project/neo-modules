using System;

namespace Neo.FileStorage.Morph.Invoker
{
    public static partial class MorphContractInvoker
    {
        public static readonly byte[] MaxObjectSizeConfig = Utility.StrictUTF8.GetBytes("MaxObjectSize");
        public static readonly byte[] BasicIncomeRateConfig = Utility.StrictUTF8.GetBytes("BasicIncomeRate");
        public static readonly byte[] AuditFeeConfig = Utility.StrictUTF8.GetBytes("AuditFee");
        public static readonly byte[] EpochDurationConfig = Utility.StrictUTF8.GetBytes("EpochDuration");
        public static readonly byte[] ContainerFeeConfig = Utility.StrictUTF8.GetBytes("ContainerFee");
        public static readonly byte[] EigenTrustIterationsConfig = Utility.StrictUTF8.GetBytes("EigenTrustIterations");
        public static readonly byte[] EigenTrustAlphaConfig = Utility.StrictUTF8.GetBytes("EigenTrustAlpha");
        public static readonly byte[] InnerRingCandidateFeeConfig = Utility.StrictUTF8.GetBytes("InnerRingCandidateFee");
        public static readonly byte[] WithdrawFeeConfig = Utility.StrictUTF8.GetBytes("WithdrawFee");

        private const string ConfigMethod = "config";

        private static ulong ReadUInt64Config(this Client client, byte[] key)
        {
            InvokeResult result = client.TestInvoke(NetMapContractHash, ConfigMethod, key);
            if (result.State != VM.VMState.HALT) throw new Exception($"could not invoke method {nameof(ReadUInt64Config)}");
            return (ulong)result.ResultStack[0].GetInteger();
        }

        private static string ReadStringConfig(this Client client, byte[] key)
        {
            InvokeResult result = client.TestInvoke(NetMapContractHash, ConfigMethod, key);
            if (result.State != VM.VMState.HALT) throw new Exception($"could not invoke method {nameof(ReadStringConfig)}");
            return result.ResultStack[0].GetString();
        }

        public static ulong MaxObjectSize(this Client client)
        {
            return client.ReadUInt64Config(MaxObjectSizeConfig);
        }

        public static ulong BasicIncomeRate(this Client client)
        {
            return client.ReadUInt64Config(BasicIncomeRateConfig);
        }

        public static ulong AuditFee(this Client client)
        {
            return client.ReadUInt64Config(AuditFeeConfig);
        }

        public static ulong EpochDuration(this Client client)
        {
            return client.ReadUInt64Config(EpochDurationConfig);
        }

        public static ulong ContainerFee(this Client client)
        {
            return client.ReadUInt64Config(ContainerFeeConfig);
        }

        public static ulong EigenTrustIterations(this Client client)
        {
            return client.ReadUInt64Config(EigenTrustIterationsConfig);
        }

        public static double EigenTrustAlpha(this Client client)
        {
            return double.Parse(client.ReadStringConfig(EigenTrustAlphaConfig));
        }

        public static ulong InnerRingCandidateFee(this Client client)
        {
            return client.ReadUInt64Config(InnerRingCandidateFeeConfig);
        }

        public static ulong WithdrawFee(this Client client)
        {
            return client.ReadUInt64Config(InnerRingCandidateFeeConfig);
        }
    }
}
