// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace Neo.Plugins.RestServer.Models.Wallet
{
    /// <summary>
    /// Wallet send object.
    /// </summary>
    public class WalletSendModel
    {
        /// <summary>
        /// Asset Id
        /// </summary>
        /// <example>0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5</example>
        [Required]
        public UInt160 AssetId { get; set; } = UInt160.Zero;

        /// <summary>
        /// ScriptHash of the address in the wallet to send from.
        /// </summary>
        /// <example>0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761</example>
        [Required]
        public UInt160 From { get; set; } = UInt160.Zero;

        /// <summary>
        /// ScriptHash of the address in the wallet to send too.
        /// </summary>
        /// <example>0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761</example>
        [Required]
        public UInt160 To { get; set; } = UInt160.Zero;

        /// <summary>
        /// Amount
        /// </summary>
        /// <example>1</example>
        /// <remarks>Not user representation.</remarks>
        [Required]
        public BigInteger Amount { get; set; }

        /// <summary>
        /// Data you would like to send.
        /// </summary>
        /// <remarks>can be null or empty.</remarks>
        /// <example>SGVsbG8gV29ybGQ=</example>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// An array of the signer that will be signing the transaction.
        /// </summary>
        /// <remarks>Can be null</remarks>
        public UInt160[] Signers { get; set; } = Array.Empty<UInt160>();
    }
}
