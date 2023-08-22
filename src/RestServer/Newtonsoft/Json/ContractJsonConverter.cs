// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    internal class ContractJsonConverter : JsonConverter<ContractState>
    {
        public override ContractState ReadJson(JsonReader reader, Type objectType, ContractState existingValue, bool hasExistingValue, global::Newtonsoft.Json.JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, ContractState value, global::Newtonsoft.Json.JsonSerializer serializer)
        {
            var contractObject = new JObject()
            {
                ["id"] = value.Id,
                ["name"] = value.Manifest.Name,
                ["hash"] = value.Hash.ToString(),
                ["manifest"] = JToken.Parse(JsonConvert.SerializeObject(value.Manifest, RestServerSettings.Default.JsonSerializerSettings)),
                ["nef"] = JToken.Parse(JsonConvert.SerializeObject(value.Nef, RestServerSettings.Default.JsonSerializerSettings)),
            };
            contractObject.WriteTo(writer);
        }
    }
}
