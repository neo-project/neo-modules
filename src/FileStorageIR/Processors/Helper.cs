using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Neo.Wallets;

namespace Neo.FileStorage.InnerRing.Processors
{
    public static class Helper
    {
        public static string ToAddress(this ECPoint p, byte version)
        {
            return p.ToScriptHash().ToAddress(version);
        }

        public static UInt160 ToScriptHash(this ECPoint p)
        {
            return Contract.CreateSignatureRedeemScript(p).ToScriptHash();
        }
    }
}
