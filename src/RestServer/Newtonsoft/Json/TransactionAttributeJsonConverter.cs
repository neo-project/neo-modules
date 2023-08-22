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
    public class TransactionAttributeJsonConverter : JsonConverter<TransactionAttribute>
    {
        public override TransactionAttribute ReadJson(JsonReader reader, Type objectType, TransactionAttribute existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, TransactionAttribute value, JsonSerializer serializer)
        {
            var txAttrObject = new JObject()
            {
                ["allowMultiple"] = value.AllowMultiple,
                ["type"] = value.Type.ToString(),
            };
            txAttrObject.WriteTo(writer);
        }
    }
}
