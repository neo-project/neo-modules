// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Numerics;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class BigDecimalJsonConverter : JsonConverter<BigDecimal>
    {
        public override BigDecimal ReadJson(JsonReader reader, Type objectType, BigDecimal existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var o = JObject.Load(reader);
            return new BigDecimal(o["value"].ToObject<BigInteger>(), o["decimals"].ToObject<byte>());
        }

        public override void WriteJson(JsonWriter writer, BigDecimal value, JsonSerializer serializer)
        {
            var o = new JObject()
            {
                new JProperty("value", value.Value),
                new JProperty("decimals", value.Decimals),
            };
            o.WriteTo(writer);
        }
    }
}
