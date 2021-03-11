using Neo.Cryptography.ECC;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using RandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator;

namespace Neo.Plugins
{

    public partial class RpcSystemAssetTrackerPlugin
    {
        private JObject CreateAddress()
        {
            var privateKey = new byte[32];

            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(privateKey);
            }

            var kp = new KeyPair(privateKey);

            var jaddress = new JObject();

            var address = kp.AsAddress();

            jaddress["wif"] = kp.Export();
            jaddress["address"] = address;
            jaddress["privkey"] = kp.PrivateKey.ToHexString();
            jaddress["pubkey"] = kp.PublicKey.ToString();
            jaddress["scripthash"] = address.ToScriptHash().ToString();

            return jaddress;
        }

        /// <summary>
        /// Derives an address and keys from given private key 
        /// </summary>
        /// <param name="privKey">HEX or WIF private key to derive address from</param>
        /// <returns>An object with address and its keys in different formats</returns>
        ///  
        private JObject GetAddress(string privKey)
        {
            JObject obj = new JObject();
            KeyPair kp = new KeyPair(privKey.ToBytePrivateKey());
            var address = kp.AsAddress();
            obj["wif"] = kp.Export();
            obj["address"] = address;
            obj["privkey"] = kp.PrivateKey.ToHexString();
            obj["pubkey"] = kp.PublicKey.ToString();
            obj["scripthash"] = address.ToScriptHash().ToString();
            return obj;
        }

        /// <summary>
        /// Allows to send system assets using a private key
        /// </summary>
        /// <param name="jArray">An array of parameters: 
        /// [0] MANDATORY; HEX or WIF private key, string
        /// [1] MANDATORY; destination address, string
        /// [2] MANDATORY; amount to send, double
        /// [3] OPTIONAL;  system token hash (hex bytes string) or its name (string). Default is CRON </param>
        /// [4] OPTIONAL;  remarks attribute value. Default is null 
        /// [5] OPTIONAL;  system fee (decimal) . Default is zero 
        /// <returns>an object with txn_hash field containing transaction hash</returns>

        private JObject Send(JArray jArray)
        {
            JObject obj = new JObject();

            var privateKey = jArray[0].AsString().Split(';').Select(x => x.ToBytePrivateKey()).ToArray();
            var addressTo = jArray[1].AsString();

            var amount = GetDecimal(jArray[2]);

            UInt256 th = (jArray.Count > 3) ? ParseTokenHash(jArray[3].AsString()) : Blockchain.UtilityToken.Hash;
            byte[] bRemarks = (jArray.Count > 4) ? jArray[4].AsString().HexToBytes() : null;
            decimal systemFee = (jArray.Count > 5) ? (decimal)jArray[5].AsNumber() : 0.0m;

            obj["txn_hash"] = SendWithKey(privateKey, amount, systemFee, addressTo, th, bRemarks);

            return obj;
        }

        /// <summary>
        /// Allows to send system assets using a private key
        /// </summary>
        /// <param name="jArray">An array of parameters: 
        /// [0] MANDATORY; HEX or WIF private key, string
        /// [1] MANDATORY; array of objects {"address": string, "amount": decimal}.
        //      At least one item is needed to send successful txn
        /// [2] OPTIONAL;  asset hash (hex bytes string) or its name (string). Default is CRON 
        /// [3] OPTIONAL;  remarks attribute value. Default is null 
        /// [4] OPTIONAL;  system fee (decimal) . Default is zero 
        /// </param>
        /// <returns>an object with txn_hash field containing transaction hash</returns>

        private JObject SendToMultipleSimple(JArray jArray)
        {
            JObject obj = new JObject();

            var privateKey = jArray[0].AsString().Split(';').Select(x => x.ToBytePrivateKey()).ToArray();
            var arrayTargets = (JArray)jArray[1];
            UInt256 th = (jArray.Count > 2) ? ParseTokenHash(jArray[2].AsString()) : Blockchain.UtilityToken.Hash;

            List<TransactionOutput> targets = new List<TransactionOutput>();

            for (int i = 0; i < arrayTargets.Count; i++)
            {
                JObject jo = arrayTargets[i];
                var addressesTo = jo["address"].AsString();
                var amount = GetDecimal(jo["amount"]);

                var target = new TransactionOutput()
                {
                    ScriptHash = addressesTo.ToScriptHash(),
                    Value = Fixed8.FromDecimal(amount),
                    AssetId = th
                };
                targets.Add(target);
            }

            byte[] bRemarks = (jArray.Count > 3) ? jArray[3].AsString().HexToBytes() : null;
            decimal systemFee = (jArray.Count > 4) ? (decimal)jArray[4].AsNumber() : 0.0m;

            KeyPair[] fromKey = privateKey.Select(x => new KeyPair(x)).ToArray();
            bool b = SendAsset(fromKey, th, targets, bRemarks, systemFee, out string txn_hash);

            obj["txn_hash"] = txn_hash;

            return obj;
        }


        private decimal GetDecimal(JObject jObject)
        {
            var d = 0.0m;
            try { d = (decimal)jObject.AsNumber(); }
            catch { d = decimal.Parse(jObject.AsString(), NumberStyles.Any, CultureInfo.InvariantCulture); }
            return d;
        }


        private UInt256 ParseTokenHash(string v)
        {
            if (string.IsNullOrWhiteSpace(v))
                return Blockchain.UtilityToken.Hash;
            v = v.Trim();
            var rv = v.ToUpper();
            if (rv == "CRON" || v.Length == 0)
                return Blockchain.UtilityToken.Hash;
            if (rv == "CRONIUM")
                return Blockchain.GoverningToken.Hash;

            var r = Blockchain.Singleton.Store.GetAssets()?.Find();
            var asset = r?.FirstOrDefault(x => x.Value.GetName().ToUpper() == rv).Value;

            if (asset != null)
                return asset.AssetId;

            return UInt256.Parse(v);
        }

        private JObject InvokeSmartContractEntryPointAs(string script, string key, JObject[] jObject)
        {
            JObject[] _params = jObject;
            UInt160 sh = UInt160.Parse(script);
            ContractState contract = Blockchain.Singleton.Store.GetContracts().TryGet(sh);
            if (contract == null)
                throw new RpcException(-1101, $"Smart contract doen't exist: {sh.ToString()}");

            ContractParameter[] parameters = contract
                .ParameterList
                .Select(p => new ContractParameter(p)).ToArray();

            for (int ip = 0; ip < parameters.Length; ip++)
                SetContractParamValue(parameters[ip],
                    ip < _params.Length ? _params[ip] : default(JObject));


            KeyPair keyPair = null;
            if (!string.IsNullOrEmpty(key))
                try { keyPair = new KeyPair(key.ToBytePrivateKey()); } catch { }

            bool b = CallContract(keyPair, script, parameters, out byte[] txhash);
            return b ? txhash.ToHexString() : null;
        }


        /// <summary>
        /// SC parameter helper function.
        /// Firstly tries to get SC parameter from type/value,
        /// then from raw value
        /// </summary>
        /// <param name="cp">destination SC parameter</param>
        /// <param name="jObj">source JObject value </param>
        /// 
        private void SetContractParamValue(ContractParameter cp, JObject jObj)
        {
            try
            {
                var cpx = ContractParameter.FromJson(jObj);
                if (cp.Type == cpx.Type)
                {
                    cp.Value = cpx.Value;
                    return;
                }
            }
            catch (Exception ex)
            {
                // Ignore all possible exceptions,
                // then treat raw value without { type, value } object 
            }

            SetFromJsonRaw(cp, jObj);
        }

        /// <summary>
        /// SC parameter helper function.
        /// Tries to get SC parameter of given type from raw value
        /// </summary>
        /// <param name="cp">destination SC parameter</param>
        /// <param name="jObj">source JObject value </param>
        /// 
        private void SetFromJsonRaw(ContractParameter cp, JObject jObj)
        {
            switch (cp.Type)
            {
                case ContractParameterType.Array:
                    cp.Value = jObj == null ?
                    (new ContractParameter[] { }).ToList() :
                    ((JArray)jObj).Select(p => ContractParameter.FromJson(p)).ToList();
                    break;

                case ContractParameterType.Boolean:
                    cp.Value = bool.Parse(jObj.AsString());
                    break;

                case ContractParameterType.Integer:
                    cp.Value = IntParse(jObj.AsString());
                    break;

                case ContractParameterType.Hash160:
                    cp.Value = UInt160.Parse(jObj.AsString());
                    break;

                case ContractParameterType.Hash256:
                    cp.Value = UInt256.Parse(jObj.AsString());
                    break;

                case ContractParameterType.Signature:
                case ContractParameterType.ByteArray:
                    cp.Value = jObj.AsString().HexToBytes();
                    break;

                case ContractParameterType.PublicKey:
                    cp.Value = ECPoint.Parse(jObj.AsString(), ECCurve.Secp256r1);
                    break;

                case ContractParameterType.String:
                    cp.Value = jObj.AsString();
                    break;

                case ContractParameterType.Map:
                    cp.Value = ((JArray)jObj).Select(p =>
                      new KeyValuePair<ContractParameter, ContractParameter>
                      (ContractParameter.FromJson(p["key"]),
                       ContractParameter.FromJson(p["value"])))
                       .ToList();
                    break;

                case ContractParameterType.InteropInterface:
                    cp.Value = jObj.AsString();
                    break;

                case ContractParameterType.Void:
                    break;

                default:
                    throw new RpcException(-1213, "Wrong parameter type");
            }
        }

        /// <summary>
        /// Parses string as long or BigInteger, 
        /// throws RpcException on error
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        private object IntParse(string v)
        {
            if (long.TryParse(v, out long num))
                return num;
            else if (BigInteger.TryParse(v.Substring(2), NumberStyles.AllowHexSpecifier, null, out BigInteger bi))
                return bi;
            throw new RpcException(-1212, "Parsing integer or BigInteger failed");
        }
    }
}