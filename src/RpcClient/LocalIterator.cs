// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO.Json;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Network.RPC
{
    public class LocalIterator
    {
        public IReadOnlyList<StackItem> Results { get; }
        public bool Truncated { get; }

        public LocalIterator(IReadOnlyList<StackItem> results, bool truncated)
        {
            Results = results;
            Truncated = truncated;
        }

        public static LocalIterator FromJson(JObject json)
        {
            if (json["type"].AsString() != "InteropInterface") throw new ArgumentException(nameof(json) + ".type");
            var iterator = json["iterator"] as JArray;
            if (iterator is null) throw new ArgumentException(nameof(json) + ".iterator");

            var items = iterator.Select(Utility.StackItemFromJson).ToList();
            var truncated = json["truncated"]?.AsBoolean() ?? false;
            return new LocalIterator(items, truncated);
        }
    }
}
