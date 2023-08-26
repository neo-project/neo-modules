// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.RestServer.Models.Node
{
    public class RemoteNodeModel
    {
        /// <summary>
        /// Remote peer's ip address.
        /// </summary>
        /// <example>10.0.0.100</example>
        public string RemoteAddress { get; set; }
        /// <summary>
        /// Remote peer's port number.
        /// </summary>
        /// <example>20333</example>
        public int RemotePort { get; set; }
        /// <summary>
        /// Remote peer's listening tcp port.
        /// </summary>
        /// <example>20333</example>
        public int ListenTcpPort { get; set; }
        /// <summary>
        /// Remote peer's last synced block height.
        /// </summary>
        /// <example>2584158</example>
        public uint LastBlockIndex { get; set; }
    }
}
