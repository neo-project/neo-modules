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
    /// Wallet import key object.
    /// </summary>
    public class WalletImportKey
    {
        /// <summary>
        /// Representation of the private.
        /// </summary>
        /// <example>L3tgppXLgdaeqSGSFw1Go3skBiy8vQAM7YMXvTHsKQtE16PBncSU</example>
        [Required(AllowEmptyStrings = false)]
        public string Wif { get; set; }
    }
}
