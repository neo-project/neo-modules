using Neo.Cryptography.ECC;
using Neo.FileStorage.Morph.Invoker;
using Neo.SmartContract;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.InnerRing.Invoker
{
    public static partial class ContractInvoker
    {
        private static UInt160 FsContractHash => Settings.Default.FsContractHash;
        private const string ChequeMethod = "cheque";
        private const string AlphabetUpdateMethod = "alphabetUpdate";

        private const long FeeHalfGas = 50_000_000;
        private const long FeeOneGas = FeeHalfGas * 2;

        public static bool CashOutCheque(this Client client, byte[] Id, long amount, UInt160 userAccount, UInt160 lockAccount)
        {
            if (client is null) throw new Exception("client is nil");
            return client.Invoke(out _, FsContractHash, ChequeMethod, ExtraFee, Id, userAccount, amount, lockAccount);
        }

        public static bool AlphabetUpdate(this Client client, byte[] Id, ECPoint[] publicKeys)
        {
            if (client is null) throw new Exception("client is nil");
            var array = new ContractParameter(ContractParameterType.Array);
            var list = new List<ContractParameter>();
            foreach (var publicKey in publicKeys)
            {
                list.Add(new ContractParameter(ContractParameterType.PublicKey) { Value = publicKey });
            }
            array.Value = list;
            return client.Invoke(out _, FsContractHash, AlphabetUpdateMethod, ExtraFee, Id, array);
        }
    }
}
