namespace Neo.FileStorage.InnerRing.Utils.Locode.Db
{
    public enum Continent : byte
    {
        ContinentUnknown = 0x00,
        ContinentEurope,
        ContinentAfrica,
        ContinentNorthAmerica,
        ContinentSouthAmerica,
        ContinentAsia,
        ContinentAntarctica,
        ContinentOceania
    }

    public static class Helper
    {
        public static string String(this Continent continent)
        {
            return continent switch
            {
                Continent.ContinentUnknown => "Unknown",
                Continent.ContinentEurope => "Europe",
                Continent.ContinentAfrica => "Africa",
                Continent.ContinentNorthAmerica => "North America",
                Continent.ContinentSouthAmerica => "South America",
                Continent.ContinentAsia => "Asia",
                Continent.ContinentAntarctica => "Antarctica",
                Continent.ContinentOceania => "Oceania",
                _ => "Unknown",
            };
        }

        public static Continent ContinentFromString(this string s)
        {
            return s switch
            {
                "Europe" => Continent.ContinentEurope,
                "Africa" => Continent.ContinentAfrica,
                "North America" => Continent.ContinentNorthAmerica,
                "South America" => Continent.ContinentSouthAmerica,
                "Asia" => Continent.ContinentAsia,
                "Antarctica" => Continent.ContinentAntarctica,
                "Australia" or "Oceania" => Continent.ContinentOceania,
                _ => Continent.ContinentUnknown,
            };
        }
    }
}
