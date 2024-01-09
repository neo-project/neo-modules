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

namespace Neo.Plugins.RestServer.Models.Wallet
{
    public class WalletTransactionScriptModel
    {
        /// <summary>
        /// Script to use.
        /// </summary>
        /// <example>CHeABTw3Q5SkjWharPAhgE+p+rGVN9FhlO4hXoJZQqA=</example>
        [Required]
        public byte[] Script { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// ScriptHash of the address in the wallet to send from.
        /// </summary>
        /// <example>0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761</example>
        [Required]
        public UInt160 From { get; set; } = UInt160.Zero;

        /// <summary>
        /// An array of the signer that will be signing the transaction.
        /// </summary>
        /// <remarks>Can be null</remarks>
        public UInt160[] Signers { get; set; } = Array.Empty<UInt160>();
    }
}
