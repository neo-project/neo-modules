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

namespace Neo.Plugins.RestServer.Models
{
    public class TransactionTransferModel
    {
        public UInt160 TokenHash { get; set; }
        public string TokenName { get; set; }
        public string TokenSymbol { get; set; }
        public byte TokenDecimals { get; set; }
        public UInt160 To { get; set; }
        public UInt160 From { get; set; }
        public BigInteger Amount { get; set; }
    }
}
