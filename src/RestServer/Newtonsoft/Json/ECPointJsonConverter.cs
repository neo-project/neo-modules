// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Neo.Cryptography.ECC;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class ECPointJsonConverter : JsonConverter<ECPoint>
    {
        public override ECPoint ReadJson(JsonReader reader, Type objectType, ECPoint existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var o = JObject.Load(reader);
            return ECPoint.Parse(o["value"].ToObject<string>(), ECCurve.Secp256r1);
        }

        public override void WriteJson(JsonWriter writer, ECPoint value, JsonSerializer serializer)
        {
            var o = new JObject()
            {
                new JProperty("type", "PublicKey"),
                new JProperty("value", value.ToString()),
            };
            o.WriteTo(writer);
        }
    }
}
