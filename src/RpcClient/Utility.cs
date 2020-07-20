using Neo.Cryptography.ECC;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Neo.Network.RPC
{
    public static class Utility
    {
        private static (BigInteger numerator, BigInteger denominator) Fraction(decimal d)
        {
            int[] bits = decimal.GetBits(d);
            BigInteger numerator = (1 - ((bits[3] >> 30) & 2)) *
                                   unchecked(((BigInteger)(uint)bits[2] << 64) |
                                             ((BigInteger)(uint)bits[1] << 32) |
                                              (uint)bits[0]);
            BigInteger denominator = BigInteger.Pow(10, (bits[3] >> 16) & 0xff);
            return (numerator, denominator);
        }

        /// <summary>
        /// Parse WIF or private key hex string to KeyPair
        /// </summary>
        /// <param name="key">WIF or private key hex string
        /// Example: WIF ("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"), PrivateKey ("450d6c2a04b5b470339a745427bae6828400cf048400837d73c415063835e005")</param>
        /// <returns></returns>
        public static KeyPair GetKeyPair(string key)
        {
            if (string.IsNullOrEmpty(key)) { throw new ArgumentNullException(nameof(key)); }
            if (key.StartsWith("0x")) { key = key.Substring(2); }

            if (key.Length == 52)
            {
                return new KeyPair(Wallet.GetPrivateKeyFromWIF(key));
            }
            else if (key.Length == 64)
            {
                return new KeyPair(key.HexToBytes());
            }

            throw new FormatException();
        }

        /// <summary>
        /// Parse address, scripthash or public key string to UInt160
        /// </summary>
        /// <param name="account">account address, scripthash or public key string
        /// Example: address ("Ncm9TEzrp8SSer6Wa3UCSLTRnqzwVhCfuE"), scripthash ("0xb0a31817c80ad5f87b6ed390ecb3f9d312f7ceb8"), public key ("02f9ec1fd0a98796cf75b586772a4ddd41a0af07a1dbdf86a7238f74fb72503575")</param>
        /// <returns></returns>
        public static UInt160 GetScriptHash(string account)
        {
            if (string.IsNullOrEmpty(account)) { throw new ArgumentNullException(nameof(account)); }
            if (account.StartsWith("0x")) { account = account.Substring(2); }

            if (account.Length == 34)
            {
                return Wallets.Helper.ToScriptHash(account);
            }
            else if (account.Length == 40)
            {
                return UInt160.Parse(account);
            }
            else if (account.Length == 66)
            {
                var pubKey = ECPoint.Parse(account, ECCurve.Secp256r1);
                return Contract.CreateSignatureRedeemScript(pubKey).ToScriptHash();
            }

            throw new FormatException();
        }

        /// <summary>
        /// Convert decimal amount to BigInteger: amount * 10 ^ decimals
        /// </summary>
        /// <param name="amount">float value</param>
        /// <param name="decimals">token decimals</param>
        /// <returns></returns>
        public static BigInteger ToBigInteger(this decimal amount, uint decimals)
        {
            BigInteger factor = BigInteger.Pow(10, (int)decimals);
            var (numerator, denominator) = Fraction(amount);
            if (factor < denominator)
            {
                throw new ArgumentException("The decimal places is too long.");
            }

            BigInteger res = factor * numerator / denominator;
            return res;
        }

        public static Block BlockFromJson(JObject json)
        {
            Block block = new Block();
            BlockBase blockBase = block;
            blockBase.FromJson(json);
            block.ConsensusData = ConsensusDataFromJson(json["consensusdata"]);
            block.Transactions = ((JArray)json["tx"]).Select(p => TransactionFromJson(p)).ToArray();
            return block;
        }

        public static void FromJson(this BlockBase block, JObject json)
        {
            block.Version = (uint)json["version"].AsNumber();
            block.PrevHash = UInt256.Parse(json["previousblockhash"].AsString());
            block.MerkleRoot = UInt256.Parse(json["merkleroot"].AsString());
            block.Timestamp = (ulong)json["time"].AsNumber();
            block.Index = (uint)json["index"].AsNumber();
            block.NextConsensus = json["nextconsensus"].AsString().ToScriptHash();
            block.Witness = ((JArray)json["witnesses"]).Select(p => WitnessFromJson(p)).FirstOrDefault();
        }

        public static Transaction TransactionFromJson(JObject json)
        {
            Transaction tx = new Transaction();
            tx.Version = byte.Parse(json["version"].AsString());
            tx.Nonce = uint.Parse(json["nonce"].AsString());
            tx.Sender = json["sender"].AsString().ToScriptHash();
            tx.SystemFee = long.Parse(json["sysfee"].AsString());
            tx.NetworkFee = long.Parse(json["netfee"].AsString());
            tx.ValidUntilBlock = uint.Parse(json["validuntilblock"].AsString());
            tx.Attributes = ((JArray)json["attributes"]).Select(p => TransactionAttributeFromJson(p)).ToArray();
            tx.Script = Convert.FromBase64String(json["script"].AsString());
            tx.Witnesses = ((JArray)json["witnesses"]).Select(p => WitnessFromJson(p)).ToArray();
            return tx;
        }

        public static Header HeaderFromJson(JObject json)
        {
            Header header = new Header();
            BlockBase blockBase = header;
            blockBase.FromJson(json);
            return header;
        }

        public static Cosigner CosignerFromJson(JObject json)
        {
            return new Cosigner
            {
                Account = UInt160.Parse(json["account"].AsString()),
                Scopes = (WitnessScope)Enum.Parse(typeof(WitnessScope), json["scopes"].AsString()),
                AllowedContracts = ((JArray)json["allowedContracts"])?.Select(p => UInt160.Parse(p.AsString())).ToArray(),
                AllowedGroups = ((JArray)json["allowedGroups"])?.Select(p => ECPoint.Parse(p.AsString(), ECCurve.Secp256r1)).ToArray()
            };
        }

        public static ConsensusData ConsensusDataFromJson(JObject json)
        {
            ConsensusData block = new ConsensusData();
            block.PrimaryIndex = (uint)json["primary"].AsNumber();
            block.Nonce = ulong.Parse(json["nonce"].AsString(), NumberStyles.HexNumber);
            return block;
        }

        public static TransactionAttribute TransactionAttributeFromJson(JObject json)
        {
            TransactionAttributeType usage = Enum.Parse<TransactionAttributeType>(json["type"].AsString());

            switch (usage)
            {
                case TransactionAttributeType.Cosigner: return CosignerFromJson(json);
                default: throw new FormatException();
            }
        }

        public static Witness WitnessFromJson(JObject json)
        {
            Witness witness = new Witness();
            witness.InvocationScript = Convert.FromBase64String(json["invocation"].AsString());
            witness.VerificationScript = Convert.FromBase64String(json["verification"].AsString());
            return witness;
        }
    }
}
