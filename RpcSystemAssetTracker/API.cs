using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Neo.Cryptography;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Ledger;
using Akka.Actor;
using Neo.Network.RPC;
using Neo.IO;

namespace Neo.Plugins
{
    public partial class RpcSystemAssetTrackerPlugin
    {

        // hard-code asset ids for CRONIUM and CRON
        public const string ASSET_CRONIUM = "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b";
        public const string ASSET_CRON = "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";
        private static Dictionary<string, string> _systemAssets;
                 

        private string SendWithKey(byte[] privateKeyFrom, decimal amount, string addressTo, UInt256 th)
        {
            KeyPair fromKey = new KeyPair(privateKeyFrom);

            bool b = SendAsset(fromKey, addressTo, th, amount, out string tx_hash);
            return tx_hash;            
        }

        public bool SendAsset(KeyPair fromKey, string toAddress, UInt256 symbol, decimal amount, out string tx_hash)
        {
            tx_hash = null;
            if (String.Equals(fromKey.AsAddress(), toAddress, StringComparison.OrdinalIgnoreCase))
            {
                throw new RpcException(-7090, "Source and dest addresses are the same");
            }

            var toScriptHash = toAddress.ToScriptHash();
            var target = new  TransactionOutput() {
                ScriptHash = toScriptHash,
                Value = Fixed8.FromDecimal(amount),
                AssetId = symbol
            };
            var targets = new List<TransactionOutput>() { target };
            return SendAsset(fromKey, symbol, targets, out tx_hash);
        }

        public bool SendAsset(KeyPair fromKey, UInt256 symbol, IEnumerable<TransactionOutput> targets, out string tx_hash)
        {
            List<CoinReference> inputs;
            List<TransactionOutput> outputs;
            GenerateInputsOutputsWithSymbol(fromKey, symbol, targets, out inputs, out outputs);
            
            ContractTransaction tx = new ContractTransaction()
            {
                Attributes = new TransactionAttribute[0],
                Version = 0,
                Inputs = inputs.ToArray(),
                Outputs = outputs.ToArray()
            };
            
            tx_hash = tx.Hash.ToString();

            return SignAndRelay(tx, fromKey);
        }

        

        public void GenerateInputsOutputsWithSymbol(KeyPair key, UInt256 symbol, 
            IEnumerable<TransactionOutput> targets, 
            out List<CoinReference> inputs, 
            out List<TransactionOutput> outputs, decimal system_fee = 0)
        {
            var from_script_hash = (key.AsSignatureScript().HexToBytes().ToScriptHash());

            List<TransactionOutput> tgts = targets?.ToList();
            if (tgts != null)
                tgts.ForEach( t => {
                    if (t.AssetId == null)
                        t.AssetId = symbol; 
                });
            //else Console.WriteLine("ASSETID target already existed: " + symbol);
            GenerateInputsOutputs(from_script_hash, tgts, out inputs, out outputs, system_fee);
        }

        public void GenerateInputsOutputs(UInt160 from_script_hash, 
            IEnumerable<TransactionOutput> targets,
            out List<CoinReference> inputs, 
            out List<TransactionOutput> outputs, decimal system_fee = 0)
        {
            var unspent = GetUnspent(from_script_hash.ToAddress());
            // filter any asset lists with zero unspent inputs
            unspent = unspent.Where(pair => pair.Value.Count > 0).ToDictionary(pair => pair.Key, pair => pair.Value);

            inputs = new List<CoinReference>();
            outputs = new List<TransactionOutput>();

            var from_address = from_script_hash.ToAddress();
            var info = GetAssetsInfo();

            // dummy tx to self
            if (targets == null)
            {
                string assetName = "CRON";
                string assetID = info[assetName];
                var targetAssetID = reverseHex(assetID).HexToBytes();
                if (!unspent.ContainsKey(assetName))
                    throw new RpcException(-7878, $"Not enough {assetName} in address {from_address}");

                var src = unspent[assetName][0];
                decimal selected = src.value;
                // Console.WriteLine("SENDING " + selected + " GAS to source");

                inputs.Add(new CoinReference()
                {
                    PrevHash = UInt256.Parse( src.txid ),
                    PrevIndex = (ushort) src.index,
                });

                outputs.Add(new TransactionOutput()
                {
                    AssetId = new UInt256(targetAssetID),
                    ScriptHash = from_script_hash,
                    Value = Fixed8.FromDecimal( selected )
                });
                return;
            }

            foreach (var target in targets)
                if (target.ScriptHash.Equals(from_script_hash))
                    throw new RpcException(-7092, "Target can't be same as input");

            //bool done_fee = false;
            foreach (var asset in info)
            {
                string assetName = asset.Key;
                string assetID = asset.Value;

                if (!unspent.ContainsKey(assetName))
                    continue;

                var targetAssetID = UInt256.Parse(assetID);

                var thistargets = targets.Where(o => o.AssetId.Equals(targetAssetID));

                decimal cost = -1;
                foreach (var target in thistargets)
                    if (target.AssetId.Equals(targetAssetID))
                    {
                        if (cost < 0)
                            cost = 0;
                        cost += decimal.Parse( target.Value.ToString());
                    }

                // incorporate fee in GAS utxo, if sending GAS
                bool sendfee = false;
                if (system_fee > 0 && assetName == "CRON")
                {
                    //done_fee = true;
                    sendfee = true;
                    if (cost < 0)
                        cost = 0;
                    cost += system_fee;
                }

                if (cost == -1)
                    continue;

                var sources = unspent[assetName].OrderBy(src => src.value);
                decimal selected = 0;

                // >= cost ou > cost??
                foreach (var src in sources)
                {
                    if (selected >= cost && inputs.Count > 0)
                        break;

                    selected += src.value;
                    inputs.Add(new CoinReference
                    {
                        PrevHash = UInt256.Parse( src.txid ),
                        PrevIndex = (ushort) src.index,
                    });
                    // Console.WriteLine("ADD inp " + src.ToString());
                }

                if (selected < cost)
                    throw new RpcException(-7878, $"Not enough {assetName} in address {from_address}");

                if (cost > 0)
                    foreach (var target in thistargets)
                        outputs.Add(target);

                if (selected > cost || cost == 0 || sendfee)  /// is sendfee needed? yes if selected == cost
                    outputs.Add(new TransactionOutput()
                    {
                        AssetId = targetAssetID,
                        ScriptHash = from_script_hash,
                        Value = Fixed8.FromDecimal(selected - cost)
                    });
            }
          

        }

        internal static Dictionary<string, string> GetAssetsInfo()
        {
            if (_systemAssets == null)
            {
                _systemAssets = new Dictionary<string, string>();
                AddAsset("CRONIUM",  ASSET_CRONIUM);
                AddAsset("CRON", ASSET_CRON);
            }

            return _systemAssets;
        }

        private static void AddAsset(string symbol, string hash)
        {
            _systemAssets[symbol] = hash;
        }

        public bool CallContract(KeyPair key, string scriptHash, ContractParameter[] args, out byte[] txhash)
        {
            var bytes = EmitAppCall(UInt160.Parse(scriptHash), args, null);
            return CallContract(key, scriptHash, bytes, out txhash);
        }

        /// <summary>
        /// Emits AppCall
        /// </summary>
        /// <param name="sh">Script hash to encode</param>
        /// <param name="parameters">Parameters to encode</param>
        /// <param name="operation">Method name to encode</param>
        /// <returns>Output script bytecode</returns>
        private byte[] EmitAppCall(UInt160 sh, ContractParameter[] parameters, string operation = null)
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                if (string.IsNullOrEmpty(operation))
                    sb.EmitAppCall(sh, parameters);
                else
                    sb.EmitAppCall(scriptHash: sh, operation: operation, args: parameters);
                script = sb.ToArray();
            }
            return script;
        }
        

        public bool CallContract(KeyPair key, string scriptHash, byte[] bytes, out byte[] txhash)
        {
            var unspent = GetUnspent(key.AsAddress());

            if (!unspent.ContainsKey("CRON"))
            {
                throw new RpcException(-3227, "No CRONs available");
            }

            var sources = unspent["CRON"];
            var inputs = new List<CoinReference>();
            var outputs = new List<TransactionOutput>();         
            decimal gasCost = 0;

            decimal selectedGas = 0;
            foreach (var src in sources)
            {
                selectedGas += src.value;

                var input = new CoinReference()
                {
                    PrevHash = UInt256.Parse( src.txid ),
                    PrevIndex = (ushort) src.index,
                };

                inputs.Add(input);

                if (selectedGas >= gasCost)
                {
                    break;
                }
            }

            if (selectedGas < gasCost)
            {
                throw new RpcException(-3228, "Not enough CRONs available");
            }

            var targetAssetID = reverseHex(ASSET_CRON).HexToBytes();

            if (selectedGas > gasCost)
            {
                var left = selectedGas - gasCost;

                var change = new  TransactionOutput()
                {
                    AssetId = new UInt256(targetAssetID),
                    ScriptHash = key.AsSignatureScript().HexToBytes().ToScriptHash(),
                    Value = Fixed8.FromDecimal(left)
                };
                outputs.Add(change);
            }

            InvocationTransaction tx = new InvocationTransaction()
            {                
                Attributes = new TransactionAttribute[0],
                Version = 0,
                Script = bytes,
                Gas = Fixed8.FromDecimal( gasCost ),
                Inputs = inputs.ToArray(),
                Outputs = outputs.ToArray()
            };

            txhash = tx.Hash.ToArray().Reverse().ToArray();
                        
            return SignAndRelay(tx, key);            
        }

        private bool SignAndRelay(Transaction tx, KeyPair key)
        {
            byte[] signature = tx.Sign(key);

            var invocationScript = "40" + signature.ToHexString();
            var verificationScript = key.AsSignatureScript();
            tx.Witnesses =
                new Witness[]
                { new Witness()
                {
                  InvocationScript = invocationScript.HexToBytes(),
                  VerificationScript = verificationScript.HexToBytes() }
                };

            RelayResultReason reason = System.Blockchain.Ask<RelayResultReason>(tx).Result;
            return GetRelayResult(reason);
        }

        private static bool GetRelayResult(RelayResultReason reason)
        {
            switch (reason)
            {
                case RelayResultReason.Succeed:
                    return true;
                case RelayResultReason.AlreadyExists:
                    throw new RpcException(-501, "Block or transaction already exists and cannot be sent repeatedly.");
                case RelayResultReason.OutOfMemory:
                    throw new RpcException(-502, "The memory pool is full and no more transactions can be sent.");
                case RelayResultReason.UnableToVerify:
                    throw new RpcException(-503, "The block cannot be validated.");
                case RelayResultReason.Invalid:
                    throw new RpcException(-504, "Block or transaction validation failed.");
                case RelayResultReason.PolicyFail:
                    throw new RpcException(-505, "One of the Policy filters failed.");
                default:
                    throw new RpcException(-500, "Unknown error.");
            }
        }

        #region NEEDS CLEANUP

        private static string reverseHex(string hex)
        {

            string result = "";
            for (var i = hex.Length - 2; i >= 0; i -= 2)
            {
                result += hex.Substring(i, 2);
            }
            return result;
        }
        

        #endregion
  

        public struct UnspentEntry
        {
            public string txid;
            public uint index;
            public decimal value;
        }        

        public Dictionary<string, List<UnspentEntry>> GetUnspent(string address)
        {
            JObject unspent = ProcessGetUnspents(new JArray(address));
            
            var result = new Dictionary<string, List<UnspentEntry>>();
            foreach (var node in (JArray) unspent["balance"])
            {
                var child = (JArray) node["unspent"];
                if (child != null)
                {
                    List<UnspentEntry> list;
                    string sym = node["asset_symbol"].AsString();
                    if (result.ContainsKey(sym))
                    {
                        list = result[sym];
                    }
                    else
                    {
                        list = new List<UnspentEntry>();
                        result[sym] = list;
                    }

                    foreach (var utxo in child)
                    {
                        var input = new UnspentEntry()
                        {
                            txid = utxo["txid"].AsString(),
                            index = (uint)utxo["n"].AsNumber(),
                            value = (decimal)utxo["value"].AsNumber()
                        };

                        list.Add(input);
                    }
                }
            }
            return result;
        }
    }

    public static class _JeePluginExt_
    {
        public static string AsAddress(this KeyPair kp)
        {
            return Contract
                .CreateSignatureRedeemScript(kp.PublicKey)
                .ToScriptHash()
                .ToAddress();
        }

        public static string AsSignatureScript(this KeyPair kp)
        {
            var bytes = kp.PublicKey.EncodePoint(true);
            return ("21" + bytes.ToHexString() + "ac");
        }


        public static byte[] ToBytePrivateKey(this string k1)
        {
            try
            {
                return Wallet.GetPrivateKeyFromWIF(k1);
            }
            catch
            {
                return k1.HexToBytes();
            }
        }
    }
}
