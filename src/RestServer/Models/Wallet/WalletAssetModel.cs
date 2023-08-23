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

namespace Neo.Plugins.RestServer.Models.Wallet
{
    public class WalletAssetModel
    {
        public string Address { get; set; }
        public UInt160 ScriptHash { get; set; }
        public ECPoint PublicKey { get; set; }
        public BigDecimal Neo { get; set; }
        public UInt160 NeoHash { get; set; }
        public BigDecimal Gas { get; set; }
        public UInt160 GasHash { get; set; }
    }
}
