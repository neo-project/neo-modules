using Neo.ConsoleService;
using Neo.FileStorage.Utils.Locode;
using Neo.FileStorage.Utils.Locode.Db;
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
        public const string ReourcePath = "./Resources/";
        public const string DefaultTargetPath = "./Data_UNLOCODE";


        [ConsoleCommand("fs generate", Category = "FileStorage", Description = "generate UN/LOCODE database for NeoFS using specified paths")]
        private void OnGenerate(string tableInPaths, string tableSubDivPath, string airportsPath, string countriesPath, string continentsPath, string targetDBPath)
        {
            CSVTable locodeDB = new(tableInPaths.Split(","), tableSubDivPath);
            AirportsDB airportsDB = new()
            {
                AirportsPath = airportsPath,
                CountriesPath = countriesPath
            };
            ContinentDB continentDB = new()
            {
                Path = continentsPath
            };
            StorageDB targetDb = new(targetDBPath);
            targetDb.FillDatabase(locodeDB, airportsDB, continentDB);
        }

        [ConsoleCommand("fs generate default", Category = "FileStorage", Description = "generate UN/LOCODE database for NeoFS using default resources")]
        private void OnGenerate()
        {
            string[] tableInPaths = new string[]
            {
                ReourcePath + "2020-2 UNLOCODE CodeListPart1.csv",
                ReourcePath + "2020-2 UNLOCODE CodeListPart2.csv",
                ReourcePath + "2020-2 UNLOCODE CodeListPart3.csv",
            };
            string tableSubDivPath = ReourcePath + "2020-2 SubdivisionCodes.csv";
            string airportsPath = ReourcePath + "airports.dat";
            string countriesPath = ReourcePath + "countries.dat";
            string continentsPath = ReourcePath + "continents.geojson";
            CSVTable locodeDB = new(tableInPaths, tableSubDivPath);
            AirportsDB airportsDB = new()
            {
                AirportsPath = airportsPath,
                CountriesPath = countriesPath
            };
            ContinentDB continentDB = new()
            {
                Path = continentsPath
            };
            StorageDB targetDb = new(DefaultTargetPath);
            targetDb.FillDatabase(locodeDB, airportsDB, continentDB);
        }
    }
}
