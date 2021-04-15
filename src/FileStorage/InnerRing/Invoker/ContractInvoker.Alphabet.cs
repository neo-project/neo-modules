using Neo.Cryptography.ECC;
using Neo.FileStorage.Morph.Invoker;
using Neo.IO;
using System.Linq;

namespace Neo.FileStorage.InnerRing.Invoker
{
    public partial class ContractInvoker
    {
        private static UInt160[] AlphabetContractHash => Settings.Default.AlphabetContractHash;
        private const string EmitMethod = "emit";
        private const string VoteMethod = "vote";

        public static bool AlphabetEmit(Client client, int index)
        {
            return client.Invoke(out _, AlphabetContractHash[index], EmitMethod, 0);
        }
        public static bool AlphabetVote(Client client, int index, ulong epoch, ECPoint[] publicKeys)
        {
            return client.Invoke(out _, AlphabetContractHash[index], VoteMethod, FeeOneGas, epoch, publicKeys.Select(p => p.ToArray()).ToArray());
        }
    }
}
