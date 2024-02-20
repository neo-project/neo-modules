// Copyright (C) 2015-2024 The Neo Project.
//
// RocksDBStorePlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;

namespace Neo.Plugins.Storage
{
    public class RocksDBStorePlugin : Plugin, IStoreProvider
    {
        public override string Description => "Uses RocksDBStore to store the blockchain data";

        public RocksDBStorePlugin()
        {
            StoreFactory.RegisterProvider(this);
        }

        /// <summary>
        /// Get store
        /// </summary>
        /// <returns>RocksDbStore</returns>
        public IStore GetStore(string path)
        {
            return new RocksDBStore(path);
        }
    }
}
