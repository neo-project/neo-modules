// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.Plugins.RestServer.Exceptions;
using Newtonsoft.Json;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class ECPointJsonConverter : JsonConverter<ECPoint>
    {
        public override ECPoint ReadJson(JsonReader reader, Type objectType, ECPoint existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var value = reader?.Value.ToString();
            try
            {
                return ECPoint.Parse(value, ECCurve.Secp256r1);
            }
            catch (FormatException)
            {
                throw new UInt256FormatException($"{value} is invalid.");
            }
        }

        public override void WriteJson(JsonWriter writer, ECPoint value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }
    }
}
