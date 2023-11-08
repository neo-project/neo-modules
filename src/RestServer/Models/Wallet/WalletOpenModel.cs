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
    /// <summary>
    /// Open wallet request object.
    /// </summary>
    public class WalletOpenModel
    {
        /// <summary>
        /// Path of the wallet file relative to the neo-cli path.
        /// </summary>
        /// <example>./wallets/mywallet.json</example>
        [Required(AllowEmptyStrings = false)]
        public string Path { get; set; }
        /// <summary>
        /// Password to open the wallet file.
        /// </summary>
        /// <example>Password1!</example>
        [Required(AllowEmptyStrings = false)]
        public string Password { get; set; }
    }
}
