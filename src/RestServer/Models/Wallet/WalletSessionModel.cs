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
    /// Open/Created wallet session object.
    /// </summary>
    public class WalletSessionModel
    {
        /// <summary>
        /// Session id for an open/created wallet.
        /// </summary>
        /// <example>066843daf5ce45aba803587780998cdb</example>
        public Guid SessionId { get; set; }
    }
}
