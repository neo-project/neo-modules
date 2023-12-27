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
    /// <summary>
    /// Wallet address object.
    /// </summary>
    public class WalletAddressModel
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
        /// Public key of the wallet address.
        /// </summary>
        /// <example>03cdb067d930fd5adaa6c68545016044aaddec64ba39e548250eaea551172e535c</example>
        public ECPoint Publickey { get; set; }
        /// <summary>
        /// has a private key or not.
        /// </summary>
        /// <example>true</example>
        public bool HasKey { get; set; }
        /// <summary>
        /// Address type.
        /// </summary>
        /// <example>Standard/MultiSignature/WatchOnly</example>
        public string Type { get; set; }
        /// <summary>
        /// The display name for the address.
        /// </summary>
        /// <example>Default Account</example>
        public string Label { get; set; }
        /// <summary>
        /// is the address a WatchOnly address.
        /// </summary>
        /// <example>false</example>
        public bool WatchOnly { get; set; }
    }
}
