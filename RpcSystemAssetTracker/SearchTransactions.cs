using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System;
using System.Linq;

namespace Neo.Plugins
{

    public partial class RpcSystemAssetTrackerPlugin
    {

        public JObject SearchScenarioZero(JArray parameters)
        {
            string address = Try(() => { return parameters[1].AsString();} );
            byte[] contains = Try(() => { return parameters[2].AsString().HexToBytes(); });
            TransactionType type = Try(() => { return (TransactionType)(int)parameters[3].AsNumber(); });
            TransactionAttributeUsage? usage = Try(() => {  return (TransactionAttributeUsage?)(int)parameters[4].AsNumber(); });
            int lengthOutMin = Try(() => { return (int)parameters[5].AsNumber(); });
            int lengthOutMax = Try(() => { return (int)parameters[6].AsNumber(); });
            int flags = Try(() => { return (int)parameters[7].AsNumber(); });

            var txns = Blockchain.Singleton.GetSnapshot()
                .Transactions.Find()
                .Where(x => 
                   (x.Value.Transaction.Type == type)
                && ((0 == (flags & 1)) || x.Value.Transaction.Outputs.Length > lengthOutMin)
                && ((0 == (flags & 2)) || x.Value.Transaction.Outputs.Length < lengthOutMax )
                && ((0 == (flags & 4)) || ContainsInput(x.Value.Transaction.Inputs, address) )
                && ((0 == (flags & 8)) || ContainsOutput(x.Value.Transaction.Outputs, address))
                && ((0 == (flags & 16)) || usage == null || SearchBytes(GetAttributeByUsage(x.Value, usage.Value), contains) != -1));

            if(128 == (flags & 128))
               txns = txns.OrderByDescending(x => x.Value.Transaction.Outputs.Length);

            JArray ja = new JArray();
            foreach (var tx in txns)
            {
                JObject jo = new JObject();
                jo["hash"] = tx.Value.Transaction.Hash.ToString();
                if ((usage != null) && (16 == (flags & 16))) 
                    jo["attribute"] = GetAttributeByUsage(tx.Value, usage.Value).ToHexString();
                jo["fee"] = new JNumber((double)tx.Value.Transaction.NetworkFee);
                jo["outputs"] = tx.Value.Transaction.Outputs.Length;
                jo["inputs"] = tx.Value.Transaction.Inputs.Length;
                ja.Add(jo);
            }            
            return ja;
        }

        private T Try<T>(Func<T> p)
        {
            try { return p(); }
            catch { return default(T); }
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

        private bool ContainsOutput(TransactionOutput[] outputs, string address)
        {
            var sh = address.ToScriptHash();
            return outputs.Any(x => x.ScriptHash == sh);
        }

        private bool ContainsInput(CoinReference[] inputs, string address)
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
            if (haystack == null) return -1;
            if (needle == null) return -1;
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