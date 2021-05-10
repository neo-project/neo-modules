using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Neo.FileStorage.Utils.locode
{
    public class Column
    {
        public class LOCODE
        {
            private string[] values = new string[2];

            public LOCODE(string[] values)
            {
                this.values = values;
            }

            public string CountryCode() { return values[0]; }
            public string LocationCode() { return values[1]; }

            public static LOCODE FromString(string s)
            {
                string locationSeparator = " ";
                string[] words = s.Split(locationSeparator);
                if (words.Length != 1 && words.Length != 2) throw new Exception("invalid string format in UN/Locode");
                return new LOCODE(words);
            }
        }
        public class LocationCode
        {
            public const int LocationCodeLen = 3;
            private char[] values = new char[LocationCodeLen];

            public LocationCode(char[] values)
            {
                this.values = values;
            }

            public char[] Symbols()
            {
                return values;
            }

            public static LocationCode LocationCodeFromString(string s)
            {
                string pattern = @"^[A-Z0-9]+$";
                if (s.Length != LocationCodeLen) throw new Exception("invalid string format in UN/Locode");
                if (!Regex.IsMatch(s, pattern)) throw new Exception("invalid string format in UN/Locode");
                return new LocationCode(s.ToCharArray());
            }
        }
        public class CountryCode
        {
            public const int CountryCodeLen = 2;
            private char[] values = new char[CountryCodeLen];

            public CountryCode(char[] values)
            {
                this.values = values;
            }

            public char[] Symbols()
            {
                return values;
            }

            public static CountryCode CountryCodeFromString(string s)
            {
                string pattern = @"^[A-Z]+$";
                if (s.Length != CountryCodeLen) throw new Exception("invalid string format in UN/Locode");
                if (!Regex.IsMatch(s, pattern)) throw new Exception("invalid string format in UN/Locode");
                return new CountryCode(s.ToCharArray());
            }

            public override string ToString()
            {
                return string.Join("", Symbols());
            }
        }
        public class CordinateCode
        {
            public const int MinutesDigits = 2;
            public const int HemisphereSymbols = 1;
            public const int LatDegDigits = 2;
            public const int LngDegDigits = 3;
            private int degDigits;
            private byte[] value;

            public static CordinateCode CoordinateFromString(string s, int degDigits, byte[] hemisphereAlphabet)
            {
                if (s.Length != degDigits + MinutesDigits + HemisphereSymbols) throw new Exception("invalid string format in UN/Locode");
                string pattern = @"^[0-9]+$";
                if (!Regex.IsMatch(s.Substring(0, degDigits + MinutesDigits), pattern)) throw new Exception("invalid string format in UN/Locode");
                foreach (var sys in s.Substring(degDigits + MinutesDigits))
                    for (int j = 0; j < hemisphereAlphabet.Length; j++)
                        if (hemisphereAlphabet[j].Equals(sys)) throw new Exception("invalid string format in UN/Locode");
                return new CordinateCode() { degDigits = degDigits, value = System.Text.Encoding.UTF8.GetBytes(s) };
            }

            public byte[] Hemisphere()
            {
                return value.Take(degDigits + MinutesDigits).ToArray();
            }

            public byte[] Degrees()
            {
                return value.Take(degDigits).ToArray();
            }

            public byte[] Minutes()
            {
                return value.Skip(degDigits).ToArray();
            }
        }
        public class LongitudeCode : CordinateCode
        {
            public static LongitudeCode LongitudeFromString(string s)
            {
                return (LongitudeCode)CoordinateFromString(s, LngDegDigits, new byte[] { (byte)'W', (byte)'E' });
            }

            public bool East()
            {
                return Hemisphere()[0] == 'E';
            }
        };
        public class LatitudeCode : CordinateCode
        {
            public static LatitudeCode LatitudeFromString(string s)
            {
                return (LatitudeCode)CoordinateFromString(s, LatDegDigits, new byte[] { (byte)'N', (byte)'S' });
            }
            public bool North()
            {
                return Hemisphere()[0] == 'N';
            }
        };
        public class Coordinates
        {
            public LatitudeCode lat;
            public LongitudeCode lng;

            public static Coordinates CoordinatesFromString(string s)
            {
                if (s.Length == 0) return null;
                var strs = s.Split(" ");
                if (strs.Length != 2) throw new Exception("invalid string format in UN/Locode");
                var lat = LatitudeCode.LatitudeFromString(strs[0]);
                var lng = LongitudeCode.LongitudeFromString(strs[1]);
                return new Coordinates() { lat = lat, lng = lng };
            }
        };
    }
}
