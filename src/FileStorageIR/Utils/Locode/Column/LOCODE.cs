using System;

namespace Neo.FileStorage.InnerRing.Utils.Locode.Column
{
    public class LOCODE
    {
        public readonly string CountryCode;
        public readonly string LocationCode;

        public LOCODE(string countryCode, string locationCode)
        {
            this.CountryCode = countryCode;
            this.LocationCode = locationCode;
        }

        public static LOCODE FromString(string s)
        {
            string locationSeparator = " ";
            string[] words = s.Split(locationSeparator);
            if (words.Length != 1 && words.Length != 2) throw new FormatException("invalid string format in UN/Locode");
            return new LOCODE(words[0], words[1]);
        }

        public override string ToString()
        {
            return CountryCode + " " + LocationCode;
        }
    }
}
