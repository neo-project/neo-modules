// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Numerics;

namespace Neo.Plugins.RestServer.Models.Wallet
{
    /// <summary>
    /// Wallet account balance object.
    /// </summary>
    internal class WalletAccountBalanceModel
    {
        /// <summary>
        /// Balance of the account.
        /// </summary>
        /// <example>10000000</example>
        public BigInteger Balance { get; set; }
    }
}
