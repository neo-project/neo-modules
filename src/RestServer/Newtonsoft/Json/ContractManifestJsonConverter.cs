// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract.Manifest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class ContractManifestJsonConverter : JsonConverter<ContractManifest>
    {
        public override ContractManifest ReadJson(JsonReader reader, Type objectType, ContractManifest existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, ContractManifest value, JsonSerializer serializer)
        {
            var manifestObject = new JObject()
            {
                ["name"] = value.Name,
                ["abi"] = JToken.Parse(JsonConvert.SerializeObject(value.Abi, RestServerSettings.Default.JsonSerializerSettings)),
                ["groups"] = JToken.Parse(JsonConvert.SerializeObject(value.Groups, RestServerSettings.Default.JsonSerializerSettings)),
                ["supportedStandards"] = JToken.Parse(JsonConvert.SerializeObject(value.SupportedStandards, RestServerSettings.Default.JsonSerializerSettings)),
                ["Extra"] = value.Extra != null ? JToken.Parse(value.Extra.ToString()) : JValue.CreateNull(),
            };
            manifestObject.WriteTo(writer);
        }
    }
}
