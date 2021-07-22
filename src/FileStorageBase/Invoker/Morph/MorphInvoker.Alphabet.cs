using System.Collections.Generic;
using Neo.Cryptography.ECC;
using Neo.SmartContract;

namespace Neo.FileStorage.Invoker.Morph
{
    public partial class MorphInvoker
    {
        private const string EmitMethod = "emit";
        private const string VoteMethod = "vote";


        public void AlphabetEmit(int index)
        {
            Invoke(AlphabetContractHash[index], EmitMethod, 0);
        }

        public void AlphabetVote(int index, ulong epoch, ECPoint[] publicKeys)
        {
            var array = new ContractParameter(ContractParameterType.Array);
            var list = new List<ContractParameter>();
            foreach (var publicKey in publicKeys)
            {
                list.Add(new ContractParameter(ContractParameterType.PublicKey) { Value = publicKey });
            }
            array.Value = list;
            Invoke(AlphabetContractHash[index], VoteMethod, SideChainFee, epoch, array);
        }
    }
}
