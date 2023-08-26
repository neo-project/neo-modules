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
    public class BlockJsonConverter : JsonConverter<Block>
    {
        public override Block ReadJson(JsonReader reader, Type objectType, Block existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, Block value, JsonSerializer serializer)
        {
            var blockObject = new JObject()
            {
                ["timestamp"] = value.Timestamp,
                ["version"] = value.Version,
                ["primaryIndex"] = value.PrimaryIndex,
                ["nonce"] = value.Nonce,
                ["index"] = value.Index,
                ["hash"] = value.Hash.ToString(),
                ["merkleRoot"] = value.MerkleRoot.ToString(),
                ["prevHash"] = value.PrevHash.ToString(),
                ["nextConsensus"] = value.NextConsensus.ToString(),
                ["witness"] = new JObject()
                {
                    ["invocationScript"] = Convert.ToBase64String(value.Witness.InvocationScript.Span),
                    ["verificationScript"] = Convert.ToBase64String(value.Witness.VerificationScript.Span),
                    ["scriptHash"] = value.Witness.ScriptHash.ToString(),
                },
                ["size"] = value.Size,
            };

            var txArray = new JArray();
            if (value.Transactions != null)
            {
                foreach (var tx in value.Transactions)
                {
                    var txObject = new JObject()
                    {
                        ["hash"] = tx.Hash.ToString(),
                        ["sender"] = tx.Sender.ToString(),
                        ["script"] = Convert.ToBase64String(tx.Script.Span),
                        ["feePerByte"] = tx.FeePerByte,
                        ["networkFee"] = tx.NetworkFee,
                        ["systemFee"] = tx.SystemFee,
                        ["nonce"] = tx.Nonce,
                        ["version"] = tx.Version,
                        ["validUntilBlock"] = tx.ValidUntilBlock,
                        ["size"] = tx.Size,
                    };

                    var witnessesArray = new JArray();
                    if (tx.Witnesses != null)
                    {
                        foreach (var witness in tx.Witnesses)
                            witnessesArray.Add(new JObject()
                            {
                                ["invocationScript"] = Convert.ToBase64String(witness.InvocationScript.Span),
                                ["verificationScript"] = Convert.ToBase64String(witness.VerificationScript.Span),
                                ["scriptHash"] = witness.ScriptHash.ToString(),
                            });
                    }
                    txObject["witnesses"] = witnessesArray;

                    var signersArray = new JArray();
                    if (tx.Signers != null)
                    {
                        foreach (var signer in tx.Signers)
                        {
                            var signerObject = new JObject();

                            var rulesArray = new JArray();
                            if (signer.Rules != null)
                            {
                                foreach (var rule in signer.Rules)
                                    rulesArray.Add(new JObject()
                                    {
                                        ["action"] = rule.Action.ToString(),
                                        ["condition"] = rule.Condition.Type.ToString(),
                                    });
                            }
                            signerObject["rules"] = rulesArray;

                            signerObject["account"] = signer.Account.ToString();

                            var allowedContractsArray = new JArray();
                            if (signer.AllowedContracts != null)
                            {
                                foreach (var contract in signer.AllowedContracts)
                                    allowedContractsArray.Add(contract.ToString());
                            }
                            signerObject["allowedContracts"] = allowedContractsArray;

                            var allowedGroupsArray = new JArray();
                            if (signer.AllowedGroups != null)
                            {
                                foreach (var group in signer.AllowedGroups)
                                    allowedGroupsArray.Add(group.ToString());
                            }
                            signerObject["allowedGroups"] = allowedGroupsArray;

                            signerObject["scopes"] = signer.Scopes.ToString();
                            signersArray.Add(signerObject);
                        }
                    }
                    txObject["signers"] = signersArray;

                    var attributesArray = new JArray();
                    if (tx.Attributes != null)
                    {
                        foreach (var attr in tx.Attributes)
                            attributesArray.Add(new JObject()
                            {
                                ["allowMultiple"] = attr.AllowMultiple,
                                ["type"] = attr.Type.ToString(),
                            });
                    }
                    txObject["attributes"] = attributesArray;

                    txArray.Add(txObject);
                }
            }
            blockObject["transactions"] = txArray;
            blockObject.WriteTo(writer);
        }
    }
}
