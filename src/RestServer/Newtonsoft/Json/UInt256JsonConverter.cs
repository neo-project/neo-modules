// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Plugins.RestServer.Exceptions;
using Newtonsoft.Json;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class UInt256JsonConverter : JsonConverter<UInt256>
    {
        public override UInt256 ReadJson(JsonReader reader, Type objectType, UInt256 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            //var o = JObject.Load(reader);
            //return UInt256.Parse(o["value"].ToObject<string>());
            try
            {
                return UInt256.Parse(reader.ReadAsString());
            }
            catch (FormatException)
            {
                throw new UInt256FormatException();
            }
        }

        public override void WriteJson(JsonWriter writer, UInt256 value, JsonSerializer serializer)
        {
            //var o = new JObject()
            //{
            //    new JProperty("type", "Hash256"),
            //    new JProperty("value", value.ToString()),
            //};
            //o.WriteTo(writer);
            writer.WriteValue(value.ToString());
        }
    }
}
