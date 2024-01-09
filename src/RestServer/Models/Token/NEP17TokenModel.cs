// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Numerics;

namespace Neo.Plugins.RestServer.Models.Token
{
    internal class NEP17TokenModel
    {
        public string Name { get; set; } = string.Empty;
        public UInt160 ScriptHash { get; set; } = UInt160.Zero;
        public string Symbol { get; set; } = string.Empty;
        public byte Decimals { get; set; }
        public BigInteger TotalSupply { get; set; }
    }
}
