// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Plugins.Storage.LevelDBStore is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using System;
using System.Linq;

namespace Neo.Plugins.Storage
{
    public class LevelDBStore : Plugin, IStorageProvider
    {
        public override string Description => "Uses LevelDB to store the blockchain data";

        public IStore GetStore(string path)
        {
            if (Environment.CommandLine.Split(' ').Any(p => p == "/repair" || p == "--repair"))
                DB.Repair(path, Options.Default);
            return new Store(path);
        }
    }
}
