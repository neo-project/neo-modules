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
    public class SignerJsonConverter : JsonConverter<Signer>
    {
        public override Signer ReadJson(JsonReader reader, Type objectType, Signer existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, Signer value, JsonSerializer serializer)
        {
            var signerObject = new JObject();

            var rulesArray = new JArray();
            foreach (var rule in value.Rules)
                rulesArray.Add(new JObject()
                {
                    ["action"] = rule.Action.ToString(),
                    ["condition"] = rule.Condition.Type.ToString(),
                });
            signerObject["rules"] = rulesArray;

            signerObject["account"] = value.Account.ToString();

            var allowedContractsArray = new JArray();
            foreach (var contract in value.AllowedContracts)
                allowedContractsArray.Add(contract.ToString());
            signerObject["allowedContracts"] = allowedContractsArray;

            var allowedGroupsArray = new JArray();
            foreach (var group in value.AllowedGroups)
                allowedGroupsArray.Add(group.ToString());
            signerObject["allowedGroups"] = allowedGroupsArray;

            signerObject["scopes"] = value.Scopes.ToString();
            signerObject.WriteTo(writer);
        }
    }
}
