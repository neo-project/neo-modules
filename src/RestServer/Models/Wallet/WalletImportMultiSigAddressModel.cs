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
using System.ComponentModel.DataAnnotations;

namespace Neo.Plugins.RestServer.Models.Wallet
{
    /// <summary>
    /// Import Multi-Signature Address Object.
    /// </summary>
    public class WalletImportMultiSigAddressModel
    {
        /// <summary>
        /// Minimum required signatures to sign.
        /// </summary>
        /// <example>2</example>
        public ushort RequiredSignatures { get; set; }
        /// <summary>
        /// Array of public keys of the addresses.
        /// </summary>
        [Required]
        public ECPoint[] PublicKeys { get; set; }
    }
}
