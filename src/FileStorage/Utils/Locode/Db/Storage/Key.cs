using System.IO;
using Neo.FileStorage.Utils.Locode.Column;
using Neo.IO;

namespace Neo.FileStorage.Utils
{
    public class Key : ISerializable
    {
        public CountryCode CountryCode;
        public LocationCode LocationCode;

        public Key() { }

        public Key(LOCODE lc)
        {
            CountryCode = CountryCode.CountryCodeFromString(lc.CountryCode());
            LocationCode = LocationCode.LocationCodeFromString(lc.LocationCode());
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
