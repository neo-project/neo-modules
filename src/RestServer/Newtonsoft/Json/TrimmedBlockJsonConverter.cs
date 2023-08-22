// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract.Native;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    internal class TrimmedBlockJsonConverter : JsonConverter<TrimmedBlock>
    {
        public override TrimmedBlock ReadJson(JsonReader reader, Type objectType, TrimmedBlock existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, TrimmedBlock value, JsonSerializer serializer)
        {
            var o = new JObject()
            {
                ["timestamp"] = value.Header.Timestamp,
                ["version"] = value.Header.Version,
                ["primaryIndex"] = value.Header.PrimaryIndex,
                ["nonce"] = value.Header.Nonce,
                ["index"] = value.Header.Index,
                ["hash"] = value.Header.Hash.ToString(),
                ["merkleRoot"] = value.Header.MerkleRoot.ToString(),
                ["prevHash"] = value.Header.PrevHash.ToString(),
                ["nextConsensus"] = value.Header.NextConsensus.ToString(),
                ["witness"] = new JObject()
                {
                    ["invocationScript"] = Convert.ToBase64String(value.Header.Witness.InvocationScript.Span),
                    ["verificationScript"] = Convert.ToBase64String(value.Header.Witness.VerificationScript.Span),
                    ["scriptHash"] = value.Header.Witness.ScriptHash.ToString(),
                },
                ["size"] = value.Header.Size,
            };
            o.WriteTo(writer);
        }
    }
}
