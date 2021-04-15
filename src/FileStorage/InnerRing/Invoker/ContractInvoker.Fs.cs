using Neo.Cryptography.ECC;
using Neo.FileStorage.Morph.Invoker;

namespace Neo.FileStorage.InnerRing.Invoker
{
    public partial class ContractInvoker
    {
        private static UInt160 FsContractHash => Settings.Default.FsContractHash;
        private const string ChequeMethod = "cheque";
        private const string AlphabetUpdateMethod = "alphabetUpdate";

        private const long FeeHalfGas = 50_000_000;
        private const long FeeOneGas = FeeHalfGas * 2;

        public static bool CashOutCheque(Client client, byte[] Id, long amount, UInt160 userAccount, UInt160 lockAccount)
        {
            return client.Invoke(out _, FsContractHash, ChequeMethod, ExtraFee, Id, userAccount, amount, lockAccount);
        }

        public static bool AlphabetUpdate(Client client, byte[] Id, ECPoint[] list)
        {
            return client.Invoke(out _, FsContractHash, AlphabetUpdateMethod, ExtraFee, Id, list);
        }
    }
}
