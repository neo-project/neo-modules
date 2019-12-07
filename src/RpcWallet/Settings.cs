using Microsoft.Extensions.Configuration;
using Neo.SmartContract.Native;

namespace Neo.Plugins
{
    internal class Settings
    {
        public long MaxFee { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.MaxFee = (long)BigDecimal.Parse(section.GetValue("MaxFee", "0.1"), NativeContract.GAS.Decimals).Value;
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
