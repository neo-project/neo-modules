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
    public class WalletChangePasswordModel
    {
        /// <summary>
        /// Current password.
        /// </summary>
        /// <example>Password1!</example>
        [Required(AllowEmptyStrings = false)]
        public string OldPassword { get; set; }
        /// <summary>
        /// New Password.
        /// </summary>
        /// <example>HelloWorld1!</example>
        [Required(AllowEmptyStrings = false)]
        public string NewPassword { get; set; }
        /// <summary>
        /// Should create a backup file.
        /// </summary>
        /// <example>false</example>
        public bool CreateBackupFile { get; set; }
        /// <summary>
        /// if backup file exists overwrite it.
        /// </summary>
        /// <example>false</example>
        public bool OverwriteIfBackupFileExists { get; set; }
    }
}
