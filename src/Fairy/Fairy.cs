using Microsoft.Extensions.Configuration;
using Neo.SmartContract.Native;

namespace Neo.Plugins
{
    public partial class Fairy : Plugin
    {
        public override string Name => "Fairy";
        public override string Description => "Test and debug fairy transactions through RPC";

        public class Settings
        {
            public long MaxGasInvoke = (long)new BigDecimal(10M, NativeContract.GAS.Decimals).Value;
            public int MaxIteratorResultItems = 1024;
        }

        private NeoSystem? system;
        private Settings? settings;

        protected override void Configure()
        {
            IConfigurationSection config = GetConfiguration();
            settings = new Settings
            {
                MaxGasInvoke = (long)new BigDecimal(config.GetValue("MaxGasInvoke", 10M), NativeContract.GAS.Decimals).Value,
                MaxIteratorResultItems = config.GetValue("MaxIteratorResultItems", 1024),
            };
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            this.system = system;
            RpcServerPlugin.RegisterMethods(this, system.Settings.Network);
        }
    }
}
