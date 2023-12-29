using Neo.Wallets;
using System.Numerics;

namespace Neo.Plugins
{
    public static class WebSocketUtility
    {
        public static UInt160 TryParseScriptHash(string addressOrScriptHash, byte addressVersion)
        {
            if (string.IsNullOrEmpty(addressOrScriptHash))
                return UInt160.Zero;

            UInt160 scriptHash;

            if (UInt160.TryParse(addressOrScriptHash, out scriptHash) == false)
                addressOrScriptHash.TryCatch(t => scriptHash = t.ToScriptHash(addressVersion));

            return scriptHash ?? UInt160.Zero;
        }

        public static BigInteger TryParseBigInteger(string value)
        {
            if (BigInteger.TryParse(value, out var result) == false)
                return BigInteger.MinusOne;
            return result;
        }
    }
}
