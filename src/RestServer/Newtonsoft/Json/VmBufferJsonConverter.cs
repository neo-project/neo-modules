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
using Buffer = Neo.VM.Types.Buffer;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class VmBufferJsonConverter : JsonConverter<Buffer>
    {
        public override Buffer ReadJson(JsonReader reader, Type objectType, Buffer existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JToken.ReadFrom(reader);
            return RestServerUtility.StackItemFromJToken(t) as Buffer;
        }

        public override void WriteJson(JsonWriter writer, Buffer value, JsonSerializer serializer)
        {
            var t = RestServerUtility.StackItemToJToken(value, null, serializer);
            t.WriteTo(writer);
        }
    }
}
