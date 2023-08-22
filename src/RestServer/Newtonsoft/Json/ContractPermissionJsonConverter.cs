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
    public class ContractPermissionJsonConverter : JsonConverter<ContractPermission>
    {
        public override ContractPermission ReadJson(JsonReader reader, Type objectType, ContractPermission existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, ContractPermission value, JsonSerializer serializer)
        {
            var contractPermissionsObject = new JObject()
            {
                ["contract"] = value.Contract.ToString(),
                ["nethods"] = value.Methods.Count == 0 ? "*" : JToken.Parse(JsonConvert.SerializeObject(value.Methods.Select(s => s.ToString()).ToArray(), RestServerSettings.Default.JsonSerializerSettings)),
            };
            contractPermissionsObject.WriteTo(writer);
        }
    }
}
