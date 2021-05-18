using Neo.ConsoleService;
using Neo.FileStorage.Utils.locode;
using Neo.FileStorage.Utils.locode.db;
using Neo.IO.Data.LevelDB;
using Neo.Plugins;

namespace Neo.FileStorage
{
    /// <summary>
    /// The entrance of the FSNode program.
    /// Built-in an innering service to process notification events related to FS when the block is persisted.
    /// </summary>
    public partial class FileStoragePlugin : Plugin, IPersistencePlugin
    {
        [ConsoleCommand("fs generate", Category = "FileStorage", Description = "generate UN/LOCODE database for NeoFS")]
        private void OnGenerate(string tableInPaths, string tableSubDevPath, string airportsPath, string countriesPath, string continentsPath, string targetDBPath)
        {
            CSVTable locodeDB = new(tableInPaths.Split(","), tableSubDevPath);
            AirportsDB airportsDB = new()
            {
                AirportsPath = airportsPath,
                CountriesPath = countriesPath
            };
            ContinentDB continentDB = new()
            {
                Path = continentsPath
            };
            DB targetDB = DB.Open(targetDBPath, new Options { CreateIfMissing = true, FilterPolicy = Native.leveldb_filterpolicy_create_bloom(15) });
            targetDB.FillDatabase(locodeDB, airportsDB, continentDB);
        }
    }
}
