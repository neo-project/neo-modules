using Neo.FileStorage.API.Reputation;
using Neo.IO;
using Neo.SmartContract;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using ECPoint = Neo.Cryptography.ECC.ECPoint;

namespace Neo.FileStorage.Morph.Invoker
{
    public static partial class MorphContractInvoker
    {
        private static UInt160 FsIdContractHash => Settings.Default.FsIdContractHash;
        private const string KeyListingMethod = "key";
        private const string AddKeysMethod = "addKey";
        private const string RemoveKeysMethod = "removeKey";

        public static ECPoint[] InvokeAccountKeys(this Client client, byte[] owner)
        {
            InvokeResult result = client.TestInvoke(FsIdContractHash, KeyListingMethod, owner);
            if (result.State != VM.VMState.HALT) throw new Exception($"could not perform test invocation ({KeyListingMethod})");
            if (result.ResultStack.Length != 1) throw new Exception();
            VM.Types.Array items = (VM.Types.Array)result.ResultStack[0];
            IEnumerator<StackItem> itemsEnumerator = items.GetEnumerator();
            List<ECPoint> lists = new();
            while (itemsEnumerator.MoveNext())
            {
                lists.Add(itemsEnumerator.Current.GetSpan().AsSerializable<ECPoint>());
            }
            return lists.ToArray();
        }

        public static bool InvokeAddKeys(this Client client, UInt160 owner, ECPoint[] keys)
        {
            if (client is null) throw new Exception("client is nil");
            var array = new ContractParameter(ContractParameterType.Array);
            var list = new List<ContractParameter>();
            foreach (var publicKey in keys)
            {
                list.Add(new ContractParameter(ContractParameterType.PublicKey) { Value = publicKey });
            }
            array.Value = list;
            return client.Invoke(out _, FsIdContractHash, AddKeysMethod, SideChainFee, owner, array);
        }

        public static bool InvokeRemoveKeys(this Client client, UInt160 owner, ECPoint[] keys)
        {
            if (client is null) throw new Exception("client is nil");
            var array = new ContractParameter(ContractParameterType.Array);
            var list = new List<ContractParameter>();
            foreach (var publicKey in keys)
            {
                list.Add(new ContractParameter(ContractParameterType.PublicKey) { Value = publicKey });
            }
            array.Value = list;
            return client.Invoke(out _, FsIdContractHash, RemoveKeysMethod, SideChainFee, owner, array);
        }
    }
}
