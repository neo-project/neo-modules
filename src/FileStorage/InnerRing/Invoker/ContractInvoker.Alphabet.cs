using Neo.Cryptography.ECC;
using Neo.FileStorage.Morph.Invoker;
using Neo.IO;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.InnerRing.Invoker
{
    public static partial class ContractInvoker
    {
        private static UInt160[] AlphabetContractHash => Settings.Default.AlphabetContractHash;
        private const string EmitMethod = "emit";
        private const string VoteMethod = "vote";

        public static bool AlphabetEmit(this Client client, int index)
        {
            if (client is null) throw new Exception("client is nil");
            return client.Invoke(out _, AlphabetContractHash[index], EmitMethod, 0);
        }

        public static bool AlphabetVote(this Client client, int index, ulong epoch, ECPoint[] publicKeys)
        {
            if (client is null) throw new Exception("client is nil");
            var array = new ContractParameter(ContractParameterType.Array);
            var list = new List<ContractParameter>();
            foreach (var publicKey in publicKeys)
            {
                list.Add(new ContractParameter(ContractParameterType.PublicKey) { Value = publicKey });
            }
            array.Value = list;
            return client.Invoke(out _, AlphabetContractHash[index], VoteMethod, FeeOneGas, epoch, array);
        }
    }
}
