using System.Collections.Generic;
using Neo.Cryptography.ECC;
using Neo.FileStorage.Invoker;
using Neo.SmartContract;

namespace Neo.FileStorage.InnerRing.Invoker
{
    public partial class MainInvoker : ContractInvoker
    {
        public long MainChainFee { get; init; }
        public UInt160 FsContractHash { get; init; }

        private const string ChequeMethod = "cheque";
        private const string AlphabetUpdateMethod = "alphabetUpdate";

        public void CashOutCheque(byte[] Id, long amount, UInt160 userAccount, UInt160 lockAccount)
        {
            Invoke(FsContractHash, ChequeMethod, MainChainFee, Id, userAccount, amount, lockAccount);
        }

        public void AlphabetUpdate(byte[] Id, ECPoint[] publicKeys)
        {
            var array = new ContractParameter(ContractParameterType.Array);
            var list = new List<ContractParameter>();
            foreach (var publicKey in publicKeys)
            {
                list.Add(new ContractParameter(ContractParameterType.PublicKey) { Value = publicKey });
            }
            array.Value = list;
            Invoke(FsContractHash, AlphabetUpdateMethod, MainChainFee, Id, array);
        }
    }
}
