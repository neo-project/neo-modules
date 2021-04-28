using Neo.FileStorage.Utils.locode.db;
using Neo.IO;
using System.IO;
using static Neo.FileStorage.Utils.locode.Column;

namespace Neo.FileStorage.Utils
{
    public class Record : ISerializable
    {
        public string countryName;
        public string locationName;
        public string subDivName;
        public string subDivCode;
        public Point p;
        public Continent cont;

        public Record()
        {
        }

        public int Size => countryName.Length + locationName.Length + subDivName.Length + subDivCode.Length + p.Size + 1;

        public void Deserialize(BinaryReader reader)
        {
            countryName = reader.ReadString();
            locationName = reader.ReadString();
            subDivName = reader.ReadString();
            subDivCode = reader.ReadString();
            p = reader.ReadSerializable<Point>();
            cont = (Continent)reader.ReadByte();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(countryName);
            writer.Write(locationName);
            writer.Write(subDivName);
            writer.Write(subDivCode);
            writer.Write(p);
            writer.Write((byte)cont);
            writer.Flush();
        }
    }

    public class Key : ISerializable
    {
        public CountryCode countryCode;
        public LocationCode locationCode;

        public Key()
        {
        }

        public Key(LOCODE lc)
        {
            this.countryCode = CountryCode.CountryCodeFromString(lc.CountryCode());
            this.locationCode = LocationCode.LocationCodeFromString(lc.LocationCode());
        }

        public int Size => CountryCode.CountryCodeLen + LocationCode.LocationCodeLen;

        public void Deserialize(BinaryReader reader)
        {
            countryCode = new CountryCode(reader.ReadChars(CountryCode.CountryCodeLen));
            locationCode = new LocationCode(reader.ReadChars(LocationCode.LocationCodeLen));
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(countryCode.Symbols());
            writer.Write(locationCode.Symbols());
            writer.Flush();
        }
    }
}
