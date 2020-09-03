using Neo.IO.Json;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;

namespace Neo.Plugins
{
    public static class Utility
    {
        public static UInt160 ToScriptHash(this JObject value)
        {
            var addressOrScriptHash = value.AsString();

            foreach (var native in NativeContract.Contracts)
            {
                if (addressOrScriptHash.Equals(native.Name, StringComparison.InvariantCultureIgnoreCase)) return native.Hash;
            }

            return addressOrScriptHash.Length < 40 ?
                addressOrScriptHash.ToScriptHash() : UInt160.Parse(addressOrScriptHash);
        }
    }
}
