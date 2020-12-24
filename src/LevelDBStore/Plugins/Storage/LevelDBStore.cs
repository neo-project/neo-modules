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
            path = string.Format(path, ProtocolSettings.Default.Magic.ToString("X8"));
            if (Environment.CommandLine.Split(' ').Any(p => p == "/repair" || p == "--repair"))
                DB.Repair(path, Options.Default);
            return new Store(path);
        }
    }
}
