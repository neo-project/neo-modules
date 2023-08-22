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
    public class BlockHeaderJsonConverter : JsonConverter<Header>
    {
        public override Header ReadJson(JsonReader reader, Type objectType, Header existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, Header value, JsonSerializer serializer)
        {
            var blockObject = new JObject()
            {
                ["timestamp"] = value.Timestamp,
                ["version"] = value.Version,
                ["primaryIndex"] = value.PrimaryIndex,
                ["nonce"] = value.Nonce,
                ["index"] = value.Index,
                ["hash"] = value.Hash.ToString(),
                ["merkleRoot"] = value.MerkleRoot.ToString(),
                ["prevHash"] = value.PrevHash.ToString(),
                ["nextConsensus"] = value.NextConsensus.ToString(),
                ["witness"] = new JObject()
                {
                    ["invocationScript"] = Convert.ToBase64String(value.Witness.InvocationScript.Span),
                    ["verificationScript"] = Convert.ToBase64String(value.Witness.VerificationScript.Span),
                    ["scriptHash"] = value.Witness.ScriptHash.ToString(),
                },
                ["size"] = value.Size,
            };
            blockObject.WriteTo(writer);
        }
    }
}
