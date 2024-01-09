// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.VM.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class StackItemJsonConverter : JsonConverter<StackItem>
    {
        public override StackItem ReadJson(JsonReader reader, Type objectType, StackItem? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JObject.Load(reader);
            return RestServerUtility.StackItemFromJToken(t);
        }

        public override void WriteJson(JsonWriter writer, StackItem? value, JsonSerializer serializer)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            var t = RestServerUtility.StackItemToJToken(value, null, serializer);
            t.WriteTo(writer);
        }
    }
}
