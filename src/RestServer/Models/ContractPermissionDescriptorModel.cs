// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;

namespace Neo.Plugins.RestServer.Models
{
    public class ContractPermissionDescriptorModel
    {
        public ECPoint Group { get; set; }
        public UInt160 Hash { get; set; }
        public bool IsGroup { get; set; }
        public bool IsHash { get; set; }
        public bool IsWildcard { get; set; }
    }
}
