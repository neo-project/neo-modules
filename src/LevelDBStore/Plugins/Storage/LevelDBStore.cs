using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using System;
using System.Linq;

namespace Neo.Plugins.Storage
{
    public class LevelDBStore : Plugin, IStorageProvider
    {
        private string path;

        public override string Description => "Uses LevelDB to store the blockchain data";

        protected override void Configure()
        {
            path = string.Format(GetConfiguration().GetSection("Path").Value ?? "Data_LevelDB_{0}", ProtocolSettings.Default.Magic.ToString("X8"));
        }

        public IStore GetStore()
        {
            if (Environment.CommandLine.Split(' ').Any(p => p == "/repair" || p == "--repair"))
                DB.Repair(path, Options.Default);
            return new Store(path);
        }
    }
}
