using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System.Linq;

namespace Neo.Plugins
{

    public partial class RpcSystemAssetTrackerPlugin
    {

        public JObject SearchScenarioZero(JArray parameters)
        {
            string address = parameters[1].AsString();
            byte[] contains = parameters[2].AsString().HexToBytes();
            TransactionType type = (TransactionType)(int)parameters[3].AsNumber();
            TransactionAttributeUsage usage = (TransactionAttributeUsage)(int)parameters[4].AsNumber();
            int lengthOutMin = (int)parameters[5].AsNumber();
            int lengthOutMax = (int)parameters[6].AsNumber();
            int flags = (int)parameters[7].AsNumber();

            var txns = Blockchain.Singleton.GetSnapshot()
                .Transactions.Find()
                .Where(x => x.Value.Transaction.Type == type
                && x.Value.Transaction.Outputs.Length > lengthOutMin
                && x.Value.Transaction.Outputs.Length < lengthOutMax
                && Contains(x.Value.Transaction.Inputs, address)
                && ContainsCR(x.Value.Transaction.Outputs, address)
                && SearchBytes(GetAttributeByUsage(x.Value, usage), contains) != -1);

            txns = txns.OrderByDescending(x => x.Value.Transaction.Outputs.Length);

            JArray ja = new JArray();
            foreach (var tx in txns)
            {
                JObject jo = new JObject();
                jo["hash"] = tx.Value.Transaction.Hash.ToString();
                jo["attribute"] = GetAttributeByUsage(tx.Value, usage).ToHexString();
                jo["fee"] = new JNumber((double)tx.Value.Transaction.NetworkFee);
                jo["outputs"] = tx.Value.Transaction.Outputs.Length;
                ja.Add(jo);
            }

            return ja;
        }


        private JObject StatScenarioZero(JArray parameters)
        {
            JObject obj = new JObject();

            var txns = Blockchain.Singleton.GetSnapshot()
                .Transactions.Find()
                .Where(x => x.Value.Transaction.Type == TransactionType.ClaimTransaction);

            var
                issued = txns.Sum(x => x.Value.Transaction.Outputs
               .Where(z => z.AssetId == Blockchain.UtilityToken.Hash)
               .Sum(z => { return (decimal)z.Value; }));

            obj["claimed"] = new JNumber((double)issued);
            return obj;
        }




        private bool ContainsCR(TransactionOutput[] outputs, string address)
        {
            var sh = address.ToScriptHash();
            return outputs.Any(x => x.ScriptHash == sh);
        }

        private bool Contains(CoinReference[] inputs, string address)
        {
            var sh = address.ToScriptHash();
            return inputs.Any(x => x.PrevHash == sh);
        }

        private byte[] GetAttributeByUsage(TransactionState tx, TransactionAttributeUsage u)
        {
            var v = tx.Transaction.Attributes.Where(x => x.Usage == u).FirstOrDefault();
            if (v == null) return null;
            return v.Data;
        }

        static int SearchBytes(byte[] haystack, byte[] needle)
        {
            var len = needle.Length;
            var limit = haystack.Length - len;
            for (var i = 0; i <= limit; i++)
            {
                var k = 0;
                for (; k < len; k++)
                {
                    if (needle[k] != haystack[i + k]) break;
                }
                if (k == len) return i;
            }
            return -1;
        }

    }
}