// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.RestServer.Models.Wallet
{
    public class WalletAddressModel
    {
        public string Address { get; set; }
        public bool HasKey { get; set; }
        public string Label { get; set; }
        public bool WatchOnly { get; set; }
    }
}