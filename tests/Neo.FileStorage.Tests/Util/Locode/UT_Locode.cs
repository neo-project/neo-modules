using System;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Utils.Locode;
using Neo.FileStorage.Utils.Locode.Db;

namespace Neo.FileStorage.Tests.Util.Locode
{
    [TestClass]
    public class UT_Locode
    {
        [TestMethod]
        public void TestFillDatabase()
        {
            string resourcePath = "./Resources/";
            string[] tableInPaths = new string[]
            {
                resourcePath + "2020-2 UNLOCODE CodeListPart1.csv",
                resourcePath + "2020-2 UNLOCODE CodeListPart2.csv",
                resourcePath + "2020-2 UNLOCODE CodeListPart3.csv",
            };
            string tableSubDivPath = resourcePath + "2020-2 SubdivisionCodes.csv";
            string airportsPath = resourcePath + "airports.dat";
            string countriesPath = resourcePath + "countries.dat";
            string continentsPath = resourcePath + "continents.geojson";
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
            string dbPath = "./Data_LOCODE";
            StorageDB targetDb = new(dbPath);
            Console.WriteLine(Path.GetFullPath(dbPath));
            targetDb.FillDatabase(locodeDB, airportsDB, continentDB);
            targetDb.Dispose();
            //Directory.Delete(dbPath, true);
        }

        [TestMethod]
        public void TestReadCountries()
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
            };
            using (var reader = new StreamReader("./Resources/countries.dat"))
            using (var csv = new CsvReader(reader, config))
            {
                var records = csv.GetRecords<AirportsDB.Country>();
                Assert.AreEqual(records.Count(), 261);
            }
        }

        [TestMethod]
        public void TestReadEmptyRecord()
        {
            string path = "test.csv";
            string content = ",\"AD\",,\".ANDORRA\",,,,,,,,";
            File.WriteAllText(path, content);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
            };

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, config);

            var records = csv.GetRecords<CSVTable.UNLOCODERecord>();
            var record = records.First();
            Assert.AreEqual("AD", record.CountryCode);
            File.Delete(path);
        }

        [TestMethod]
        public void TestReadUNLOCODE()
        {
            string path = "./Resources/2020-2 UNLOCODE CodeListPart1.csv";
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
            };

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, config);

            var records = csv.GetRecords<CSVTable.UNLOCODERecord>();
            Assert.AreEqual(50990, records.Count());
        }
    }
}
