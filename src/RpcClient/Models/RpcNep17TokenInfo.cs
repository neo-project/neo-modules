// Copyright (C) 2016-2021 NEO GLOBAL DEVELOPMENT.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Numerics;

namespace Neo.Network.RPC.Models
{
    public class RpcNep17TokenInfo
    {
        public string Name { get; set; }

        public string Symbol { get; set; }

        public byte Decimals { get; set; }

        public BigInteger TotalSupply { get; set; }
    }
}
