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
    /// Wallet create object.
    /// </summary>
    public class WalletCreateModel
    {
        /// <summary>
        /// Account display name
        /// </summary>
        /// <example>Default Account</example>
        /// <remarks>Can be null.</remarks>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Path of the wallet file relative to the neo-cli path.
        /// </summary>
        /// <example>./wallets/mywallet.json</example>
        [Required(AllowEmptyStrings = false)]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Representation of the private.
        /// </summary>
        /// <example>L3tgppXLgdaeqSGSFw1Go3skBiy8vQAM7YMXvTHsKQtE16PBncSU</example>
        /// <remarks>Can be null or empty.</remarks>
        public string Wif { get; set; } = string.Empty;

        /// <summary>
        /// Password to open the wallet file.
        /// </summary>
        /// <example>Password1!</example>
        [Required(AllowEmptyStrings = false)]
        public string Password { get; set; } = string.Empty;
    }
}
