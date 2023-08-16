// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.RestServer.Models
{
    public class NefFileModel
    {
        public uint CheckSum { get; set; }
        public string Compiler { get; set; }
        public ReadOnlyMemory<byte> Script { get; set; }
        public string Source { get; set; }
        public IEnumerable<MethodTokenModel> Tokens { get; set; }
    }
}
