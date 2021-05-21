using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo.FileStorage.Utils.Locode.Column;
using Neo.IO;
using Neo.IO.Data.LevelDB;

namespace Neo.FileStorage.Utils.Locode.Db
{
    public class StorageDB : IDisposable
    {
        private const byte PreLocode = 0x00;
        private readonly DB _db;

        public StorageDB(string path)
        {
            _db = DB.Open(path, new Options { CreateIfMissing = true, FilterPolicy = Native.leveldb_filterpolicy_create_bloom(15) });
        }

        public void Dispose()
        {
            _db?.Dispose();
        }

        public (Key, Record) Get(LOCODE lc)
        {
            Key key = new(lc);
            Record record = _db.Get(ReadOptions.Default, key.ToArray())?.AsSerializable<Record>();
            return (key, record);
        }

        public void Put(LOCODE lc, Record record)
        {
            _db.Put(WriteOptions.Default, Key(PreLocode, new Key(lc)), record.ToArray());
        }

        public void Put(Key key, Record record)
        {
            _db.Put(WriteOptions.Default, Key(PreLocode, key), record.ToArray());
        }

        private byte[] Key(byte prefix, ISerializable key)
        {
            byte[] buffer = new byte[key.Size + 1];
            using (MemoryStream ms = new(buffer, true))
            using (BinaryWriter writer = new(ms))
            {
                writer.Write(prefix);
                key.Serialize(writer);
            }
            return buffer;
        }

        public void FillDatabase(CSVTable table, AirportsDB airports, ContinentDB continents)
        {
            table.IterateAll(tableRecord =>
            {
                if (tableRecord.LOCODE.LocationCode() == "")
                    return;
                Key dbKey = new(tableRecord.LOCODE);
                Record dbRecord = new(tableRecord);
                string countryName = "";
                if (dbRecord.Point is null)
                {
                    AirportRecord airportRecord = null;
                    try
                    {
                        airportRecord = airports.Get(tableRecord);
                    }
                    catch (KeyNotFoundException)
                    {
                        return;
                    }
                    countryName = airportRecord.CountryName;
                    dbRecord.Point = airportRecord.Point;
                }
                if (countryName == "")
                {
                    try
                    {
                        countryName = airports.CountryName(dbKey.CountryCode);
                    }
                    catch (KeyNotFoundException)
                    {
                        return;
                    }
                }
                dbRecord.CountryName = countryName;
                if (dbRecord.SubDivCode != "")
                {
                    string subDevName = "";
                    try
                    {
                        subDevName = table.SubDivName(dbKey.CountryCode, dbRecord.SubDivCode);
                    }
                    catch (KeyNotFoundException)
                    {
                        return;
                    }
                    dbRecord.SubDivName = subDevName;
                }
                var continent = continents.PointContinent(dbRecord.Point);
                if (continent == Continent.ContinentUnknown) return;
                dbRecord.Continent = continent;
                Put(dbKey, dbRecord);
            });
        }
    }
}
