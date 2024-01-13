// Copyright (C) 2015-2024 The Neo Project.
//
// WebSocketUtility.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Wallets;
using System.Numerics;

namespace Neo.Plugins.WsRpcJsonServer
{
    public static class WebSocketUtility
    {
        public static UInt160 TryParseScriptHash(string? addressOrScriptHash, byte addressVersion)
        {
            if (string.IsNullOrEmpty(addressOrScriptHash))
                return UInt160.Zero;

            if (UInt160.TryParse(addressOrScriptHash, out var scriptHash) == false)
                addressOrScriptHash.TryCatch(t => scriptHash = t.ToScriptHash(addressVersion));

            return scriptHash ?? UInt160.Zero;
        }

        public static UInt256 TryParseUInt256(string? uInt256String)
        {
            if (string.IsNullOrEmpty(uInt256String))
                return UInt256.Zero;

            _ = UInt256.TryParse(uInt256String, out var hash);

            return hash ?? UInt256.Zero;
        }


        public static BigInteger TryParseBigInteger(string value)
        {
            if (BigInteger.TryParse(value, out var result) == false)
                return BigInteger.MinusOne;
            return result;
        }
    }
}
