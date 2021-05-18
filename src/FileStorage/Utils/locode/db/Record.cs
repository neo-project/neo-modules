using System.IO;
using Neo.FileStorage.Utils.locode;
using Neo.FileStorage.Utils.locode.db;
using Neo.IO;
using static Neo.FileStorage.Utils.locode.Column;

namespace Neo.FileStorage.Utils
{
    public class Record : ISerializable
    {
        public string CountryName;
        public string LocationName;
        public string SubDivName;
        public string SubDivCode;
        public Point Point;
        public Continent Continent;

        public Record()
        {
        }

        public Record(LocodeRecord lr)
        {
            var crd = Coordinates.CoordinatesFromString(lr.Coordinates);
            Point = Point.PointFromCoordinates(crd);
            LocationName = lr.NameWoDiacritics;
            SubDivCode = lr.SubDiv;
        }

        public int Size => CountryName.Length + LocationName.Length + SubDivName.Length + SubDivCode.Length + Point.Size + 1;

        public void Deserialize(BinaryReader reader)
        {
            CountryName = reader.ReadString();
            LocationName = reader.ReadString();
            SubDivName = reader.ReadString();
            SubDivCode = reader.ReadString();
            Point = reader.ReadSerializable<Point>();
            Continent = (Continent)reader.ReadByte();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(CountryName);
            writer.Write(LocationName);
            writer.Write(SubDivName);
            writer.Write(SubDivCode);
            writer.Write(Point);
            writer.Write((byte)Continent);
            writer.Flush();
        }
    }

    public class Key : ISerializable
    {
        public CountryCode CountryCode;
        public LocationCode LocationCode;

        public Key()
        {
        }

        public Key(LOCODE lc)
        {
            this.CountryCode = CountryCode.CountryCodeFromString(lc.CountryCode());
            this.LocationCode = LocationCode.LocationCodeFromString(lc.LocationCode());
        }

        public int Size => CountryCode.CountryCodeLen + LocationCode.LocationCodeLen;

        public void Deserialize(BinaryReader reader)
        {
            CountryCode = new CountryCode(reader.ReadChars(CountryCode.CountryCodeLen));
            LocationCode = new LocationCode(reader.ReadChars(LocationCode.LocationCodeLen));
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(CountryCode.Symbols());
            writer.Write(LocationCode.Symbols());
            writer.Flush();
        }
    }
}
