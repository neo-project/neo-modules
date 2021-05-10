using System;
using System.Collections.Generic;
using System.IO;
using static Neo.FileStorage.Utils.locode.Column;

namespace Neo.FileStorage.Utils.locode.db
{
    public class AirportsDB
    {
        private const int airportCity = 2;
        private const int airportCountry = 3;
        private const int airportIATA = 4;
        private const int airportLatitude = 6;
        private const int airportLongitude = 7;
        private const int airportFldNum = 14;

        private const int countryName = 0;
        private const int countryISOCode = 1;
        private const int countryFldNum = 3;

        private Once airportsOnce = new();
        private Once countriesOnce = new();
        private string airportsPath;
        private string countriesPath;
        private Dictionary<string, string> mCountries = new();
        private Dictionary<string, List<AirportsRecord>> mAirports = new();

        public AirportsRecord Get(LocodeRecord locodeRecord)
        {
            InitAirports();
            List<AirportsRecord> records = mAirports[locodeRecord.LOCODE.CountryCode()];
            foreach (var record in records)
            {
                if (locodeRecord.LOCODE.LocationCode() != record.iata && locodeRecord.NameWoDiacritics != record.city) continue;
                return record;
            }
            throw new Exception("airport not found");
        }


        public string CountryName(CountryCode code)
        {
            InitCountries();
            string name = null;
            foreach (var country in mCountries)
            {
                if (country.Value == code.ToString())
                {
                    name = country.Key;
                    break;
                }
            }
            if (name is null) throw new Exception("country not found");
            return name;
        }

        public void InitAirports()
        {
            airportsOnce.Do(() =>
            {
                InitCountries();
                ScanWords(airportsPath, airportFldNum, (string[] words) =>
                {
                    if (mCountries.TryGetValue(words[airportCountry], out var countryCode))
                    {
                        AirportsRecord record = new AirportsRecord()
                        {
                            city = words[airportCity],
                            country = words[airportCountry],
                            iata = words[airportIATA],
                            lat = words[airportLatitude],
                            lng = words[airportLongitude],
                        };
                        if (mAirports.TryGetValue(words[airportCountry], out var records))
                            records.Add(record);
                        else
                            mAirports[words[airportCountry]] = new List<AirportsRecord>() { record };
                    }
                });
            });
        }
        public void InitCountries()
        {
            countriesOnce.Do(() =>
            {
                ScanWords(countriesPath, countryFldNum, (string[] words) =>
                {
                    mCountries[words[countryName]] = words[countryISOCode];
                });
            });
        }

        public void ScanWords(string path, int fpr, Action<string[]> wordsHandler)
        {
            string[] records = File.ReadAllLines(path);
            foreach (string record in records)
            {
                var words = record.Split(',');
                if (words.Length != fpr) throw new Exception("invalid table record");
                wordsHandler(words);
            }
        }
    }
    public class Once
    {
        private bool flag;
        private Object obj = new();

        public void Do(Action action)
        {
            if (!flag)
            {
                lock (obj)
                {
                    if (!flag)
                    {
                        flag = true;
                        action();
                    }
                }
            }
            else return;
        }
    }

    public class AirportsRecord
    {
        public string city;
        public string country;
        public string iata;
        public string lat;
        public string lng;
    }
}
