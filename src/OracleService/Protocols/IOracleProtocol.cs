// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Plugins.OracleService is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    interface IOracleProtocol : IDisposable
    {
        void Configure();
        Task<(OracleResponseCode, string)> ProcessAsync(Uri uri, CancellationToken cancellation);
    }
}
