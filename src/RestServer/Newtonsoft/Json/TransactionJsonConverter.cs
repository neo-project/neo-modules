// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class TransactionJsonConverter : JsonConverter<Transaction>
    {
        public override Transaction ReadJson(JsonReader reader, Type objectType, Transaction existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, Transaction value, JsonSerializer serializer)
        {
            var txObject = new JObject()
            {
                ["hash"] = value.Hash.ToString(),
                ["sender"] = value.Sender.ToString(),
                ["script"] = Convert.ToBase64String(value.Script.Span),
                ["feePerByte"] = value.FeePerByte,
                ["networkFee"] = value.NetworkFee,
                ["systemFee"] = value.SystemFee,
                ["nonce"] = value.Nonce,
                ["version"] = value.Version,
                ["validUntilBlock"] = value.ValidUntilBlock,
                ["size"] = value.Size,
            };

            var witnessesArray = new JArray();
            foreach (var witness in value.Witnesses)
                witnessesArray.Add(new JObject()
                {
                    ["invocationScript"] = Convert.ToBase64String(witness.InvocationScript.Span),
                    ["verificationScript"] = Convert.ToBase64String(witness.VerificationScript.Span),
                    ["scriptHash"] = witness.ScriptHash.ToString(),
                });
            txObject["witnesses"] = witnessesArray;

            var signersArray = new JArray();
            foreach (var signer in value.Signers)
            {
                var signerObject = new JObject();

                var rulesArray = new JArray();
                foreach (var rule in signer.Rules)
                    rulesArray.Add(new JObject()
                    {
                        ["action"] = rule.Action.ToString(),
                        ["condition"] = rule.Condition.Type.ToString(),
                    });
                signerObject["rules"] = rulesArray;

                signerObject["account"] = signer.Account.ToString();

                var allowedContractsArray = new JArray();
                foreach (var contract in signer.AllowedContracts)
                    allowedContractsArray.Add(contract.ToString());
                signerObject["allowedContracts"] = allowedContractsArray;

                var allowedGroupsArray = new JArray();
                foreach (var group in signer.AllowedGroups)
                    allowedGroupsArray.Add(group.ToString());
                signerObject["allowedGroups"] = allowedGroupsArray;

                signerObject["scopes"] = signer.Scopes.ToString();
                signersArray.Add(signerObject);
            }
            txObject["signers"] = signersArray;

            var attributesArray = new JArray();
            foreach (var attr in value.Attributes)
                attributesArray.Add(new JObject()
                {
                    ["allowMultiple"] = attr.AllowMultiple,
                    ["type"] = attr.Type.ToString(),
                });

            txObject["attributes"] = attributesArray;
            txObject.WriteTo(writer);
        }
    }
}
