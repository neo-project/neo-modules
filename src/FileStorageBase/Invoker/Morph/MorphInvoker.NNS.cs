using System;
using System.Linq;
using Neo.SmartContract.Native;

namespace Neo.FileStorage.Invoker.Morph
{
    public partial class MorphInvoker
    {
        private enum RecordType : int
        {
            A = 1,
            CNAME = 5,
            TXT = 16
        }

        private static UInt160 nnsScriptHash = null;
        private const string DomainName = "neofs";
        private const string NNSResolveMethod = "resolve";
        private const int NNSContractID = 1;

        public UInt160 NNSContractScriptHash(string name)
        {
            if (nnsScriptHash is null)
            {
                var contracts = NativeContract.ContractManagement.ListContracts(NeoSystem.StoreView);
                foreach (var contract in contracts)
                    if (contract.Id == NNSContractID)
                        nnsScriptHash = contract.Hash;
                if (nnsScriptHash is null) throw new InvalidOperationException("nns contract not found");
            }
            var result = TestInvoke(nnsScriptHash, NNSResolveMethod, name + "." + DomainName, RecordType.TXT);
            var arr = result.ResultStack[0] as VM.Types.Array;
            if (arr.Count == 0)
                throw new InvalidOperationException($"no record in nns, name={name}");
            var bytes = arr[0].GetSpan();
            return new UInt160(Utility.StrictUTF8.GetString(bytes).HexToBytes().Reverse().ToArray());
        }
    }
}
