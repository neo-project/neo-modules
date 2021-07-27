using System;
using System.Collections.Generic;
using Neo.SmartContract;
using Neo.VM.Types;
using ECPoint = Neo.Cryptography.ECC.ECPoint;

namespace Neo.FileStorage.Invoker.Morph
{
    public partial class MorphInvoker
    {
        private const string KeyListingMethod = "key";
        private const string AddKeysMethod = "addKey";
        private const string RemoveKeysMethod = "removeKey";

        public List<byte[]> AccountKeys(byte[] owner)
        {
            InvokeResult result = TestInvoke(FsIdContractHash, KeyListingMethod, owner);
            if (result.State != VM.VMState.HALT) throw new Exception($"could not perform test invocation ({KeyListingMethod})");
            if (result.ResultStack.Length != 1) throw new Exception();
            VM.Types.Array items = (VM.Types.Array)result.ResultStack[0];
            IEnumerator<StackItem> itemsEnumerator = items.GetEnumerator();
            List<byte[]> lists = new();
            while (itemsEnumerator.MoveNext())
            {
                lists.Add(itemsEnumerator.Current.GetSpan().ToArray());
            }
            return lists;
        }

        public void AddKeys(UInt160 owner, ECPoint[] keys)
        {
            var array = new ContractParameter(ContractParameterType.Array);
            var list = new List<ContractParameter>();
            foreach (var publicKey in keys)
            {
                list.Add(new ContractParameter(ContractParameterType.PublicKey) { Value = publicKey });
            }
            array.Value = list;
            Invoke(FsIdContractHash, AddKeysMethod, SideChainFee, owner, array);
        }

        public void RemoveKeys(UInt160 owner, ECPoint[] keys)
        {
            var array = new ContractParameter(ContractParameterType.Array);
            var list = new List<ContractParameter>();
            foreach (var publicKey in keys)
            {
                list.Add(new ContractParameter(ContractParameterType.PublicKey) { Value = publicKey });
            }
            array.Value = list;
            Invoke(FsIdContractHash, RemoveKeysMethod, SideChainFee, owner, array);
        }
    }
}
