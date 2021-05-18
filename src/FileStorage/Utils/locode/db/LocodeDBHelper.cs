using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo.IO;
using Neo.IO.Data.LevelDB;
using static Neo.FileStorage.Utils.locode.Column;

namespace Neo.FileStorage.Utils.locode.db
{
    public static class LocodeDBHelper
    {
        private const byte PreLocode = 0x00;

        public static (Key, Record) Get(this DB _db, LOCODE lc)
        {
            Key key = new Key(lc);
            Record record = _db.Get(ReadOptions.Default, key.ToArray())?.AsSerializable<Record>();
            return (key, record);
        }

        public static void Put(this DB _db, LOCODE lc, Record record)
        {
            _db.Put(WriteOptions.Default, Key(PreLocode, new Key(lc)), record.ToArray());
        }

        public static void Put(this DB _db, Key key, Record record)
        {
            _db.Put(WriteOptions.Default, Key(PreLocode, key), record.ToArray());
        }

        private static byte[] Key(byte prefix, ISerializable key)
        {
            byte[] buffer = new byte[key.Size + 1];
            using (MemoryStream ms = new MemoryStream(buffer, true))
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(prefix);
                key.Serialize(writer);
            }
            return buffer;
        }

        public static void FillDatabase(this DB db, CSVTable table, AirportsDB airports, ContinentDB continents)
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
                db.Put(dbKey, dbRecord);
            });
        }
    }
}
