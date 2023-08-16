// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Models
{
    public class ManifestModel
    {
        public string Name { get; set; }
        public ContractAbiModel Abi { get; set; }
        public IEnumerable<ContractGroupModel> Groups { get; set; }
        public IEnumerable<ContractPermissionModel> Permissions { get; set; }
        public IEnumerable<ContractPermissionDescriptorModel> Trusts { get; set; }
        public IEnumerable<string> SupportedStandards { get; set; }
        public JObject Extra { get; set; }
    }
}
