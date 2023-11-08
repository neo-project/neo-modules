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
    /// <summary>
    /// Multi-Signature Contract Object.
    /// </summary>
    internal class WalletMultiSignContractModel
    {
        /// <summary>
        /// Wallet address that was exported.
        /// </summary>
        /// <example>NNLi44dJNXtDNSBkofB48aTVYtb1zZrNEs</example>
        public string Address { get; set; }
        /// <summary>
        /// Scripthash of the wallet account exported.
        /// </summary>
        /// <example>0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761</example>
        public UInt160 ScriptHash { get; set; }
        /// <summary>
        /// Script that used to create the address
        /// </summary>
        /// <example>CHeABTw3Q5SkjWharPAhgE+p+rGVN9FhlO4hXoJZQqA=</example>
        public byte[] Script { get; set; }
    }
}
