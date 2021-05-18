using System;
using System.Collections.Generic;
using System.IO;
using static Neo.FileStorage.Utils.locode.Column;

namespace Neo.FileStorage.Utils.locode.db
{
    public class AirportsDB
    {
        public string AirportsPath { get; init; }
        public string CountriesPath { get; init; }

        private const int airportCity = 2;
        private const int airportCountry = 3;
        private const int airportIATA = 4;
        private const int airportLatitude = 6;
        private const int airportLongitude = 7;
        private const int airportFldNum = 14;

        private const int countryName = 0;
        private const int countryISOCode = 1;
        private const int countryFldNum = 3;

        private readonly Once airportsOnce = new();
        private readonly Once countriesOnce = new();
        private readonly Dictionary<string, string> mCountries = new();
        private readonly Dictionary<string, List<Record>> mAirports = new();

        private class Record
        {
            public string City;
            public string Country;
            public string Iata;
            public string Lat;
            public string Lng;
        }
        public AirportRecord Get(LocodeRecord locodeRecord)
        {
            InitAirports();
            List<Record> records = mAirports[locodeRecord.LOCODE.CountryCode()];
            foreach (var record in records)
            {
                if (locodeRecord.LOCODE.LocationCode() != record.Iata && locodeRecord.NameWoDiacritics != record.City) continue;
                return new()
                {
                    CountryName = record.Country,
                    Point = new()
                    {
                        Latitude = double.Parse(record.Lat),
                        Longitude = double.Parse(record.Lng)
                    }
                };
            }
            throw new KeyNotFoundException("airport not found");
        }


        public string CountryName(CountryCode code)
        {
            InitCountries();
            foreach (var country in mCountries)
            {
                if (country.Value == code.ToString())
                {
                    return country.Key;
                }
            }
            throw new KeyNotFoundException("country not found");
        }

        public void InitAirports()
        {
            airportsOnce.Do(() =>
            {
                InitCountries();
                ScanWords(AirportsPath, airportFldNum, (string[] words) =>
                {
                    if (mCountries.TryGetValue(words[airportCountry], out var countryCode))
                    {
                        Record record = new()
                        {
                            City = words[airportCity],
                            Country = words[airportCountry],
                            Iata = words[airportIATA],
                            Lat = words[airportLatitude],
                            Lng = words[airportLongitude],
                        };
                        if (mAirports.TryGetValue(words[airportCountry], out var records))
                            records.Add(record);
                        else
                            mAirports[words[airportCountry]] = new List<Record>() { record };
                    }
                });
            });
        }
        public void InitCountries()
        {
            countriesOnce.Do(() =>
            {
                ScanWords(CountriesPath, countryFldNum, (string[] words) =>
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

    public class AirportRecord
    {
        public string CountryName;
        public Point Point;
    }
}
