using Neo.VM.Types;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Invoker.Morph
{
    public partial class MorphInvoker
    {
        public readonly byte[] MaxObjectSizeConfig = Utility.StrictUTF8.GetBytes("MaxObjectSize");
        public readonly byte[] BasicIncomeRateConfig = Utility.StrictUTF8.GetBytes("BasicIncomeRate");
        public readonly byte[] AuditFeeConfig = Utility.StrictUTF8.GetBytes("AuditFee");
        public readonly byte[] EpochDurationConfig = Utility.StrictUTF8.GetBytes("EpochDuration");
        public readonly byte[] ContainerFeeConfig = Utility.StrictUTF8.GetBytes("ContainerFee");
        public readonly byte[] EigenTrustIterationsConfig = Utility.StrictUTF8.GetBytes("EigenTrustIterations");
        public readonly byte[] EigenTrustAlphaConfig = Utility.StrictUTF8.GetBytes("EigenTrustAlpha");
        public readonly byte[] InnerRingCandidateFeeConfig = Utility.StrictUTF8.GetBytes("InnerRingCandidateFee");
        public readonly byte[] WithdrawFeeConfig = Utility.StrictUTF8.GetBytes("WithdrawFee");

        private const string ConfigMethod = "config";
        private const string SetConfigMethod = "setConfig";
        private const string ListConfigMethod = "listConfig";

        public void SetConfig(byte[] Id, byte[] key, byte[] value)
        {
            Invoke(NetMapContractHash, SetConfigMethod, SideChainFee, Id, key, value);
        }

        private uint ReadUInt32Config(byte[] key)
        {
            InvokeResult result = TestInvoke(NetMapContractHash, ConfigMethod, key);
            return (uint)result.ResultStack[0].GetInteger();
        }

        private ulong ReadUInt64Config(byte[] key)
        {
            InvokeResult result = TestInvoke(NetMapContractHash, ConfigMethod, key);
            return (ulong)result.ResultStack[0].GetInteger();
        }

        private string ReadStringConfig(byte[] key)
        {
            InvokeResult result = TestInvoke(NetMapContractHash, ConfigMethod, key);
            return result.ResultStack[0].GetString();
        }

        public ulong MaxObjectSize()
        {
            return ReadUInt64Config(MaxObjectSizeConfig);
        }

        public ulong BasicIncomeRate()
        {
            return ReadUInt64Config(BasicIncomeRateConfig);
        }

        public ulong AuditFee()
        {
            return ReadUInt64Config(AuditFeeConfig);
        }

        public uint EpochDuration()
        {
            return ReadUInt32Config(EpochDurationConfig);
        }

        public ulong ContainerFee()
        {
            return ReadUInt64Config(ContainerFeeConfig);
        }

        public ulong EigenTrustIterations()
        {
            return ReadUInt64Config(EigenTrustIterationsConfig);
        }

        public double EigenTrustAlpha()
        {
            return double.Parse(ReadStringConfig(EigenTrustAlphaConfig));
        }

        public ulong InnerRingCandidateFee()
        {
            return ReadUInt64Config(InnerRingCandidateFeeConfig);
        }

        public ulong WithdrawFee()
        {
            return ReadUInt64Config(WithdrawFeeConfig);
        }

        public List<(byte[], byte[])> ListConfigs()
        {
            InvokeResult result = TestInvoke(NetMapContractHash, ListConfigMethod);
            if (result.ResultStack.Length != 1) throw new InvalidOperationException($"unexpected stack item, count={result.ResultStack.Length}");
            var records = (VM.Types.Array)result.ResultStack[0];
            List<(byte[], byte[])> configs = new();
            foreach (var record in records)
            {
                Map map = (Map)record;
                foreach (var (key, value) in map)
                    configs.Add((key.GetSpan().ToArray(), value.GetSpan().ToArray()));
            }
            return configs;
        }
    }
}
