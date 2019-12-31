using System.Collections.Generic;

namespace Neo.Plugins
{
    public class Assets
    {
        public string From { get; set; }
        public List<Asset> Asset { get; set; }
    }

    public class Asset
    {
        public string AssetId { get; set; }
        public string Value { get; set; }
        public string Address { get; set; }
    }
}
