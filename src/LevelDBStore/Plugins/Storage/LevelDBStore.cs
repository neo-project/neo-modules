using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins.Storage
{
    public class LevelDBStore : Plugin, IStorageProvider
    {
        public override string Description => "Uses LevelDB to store the blockchain data";

        private List<WeakReference> dbs = new();

        public IStore GetStore(string path)
        {
            if (Environment.CommandLine.Split(' ').Any(p => p == "/repair" || p == "--repair"))
                DB.Repair(path, Options.Default);
            IStore store = new Store(path);
            dbs.Add(new WeakReference(store));
            return store;
        }
        public override void Dispose()
        {
            base.Dispose();
            foreach (var a in dbs)
            {
                IStore store = a.Target as IStore;
                if (store is null) continue;
                store.Dispose();
            }
        }
    }
}
