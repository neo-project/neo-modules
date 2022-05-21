using Neo.SmartContract.Native;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public partial class Fairy : Plugin
    {
        public override string Name => "Fairy";
        public override string Description => "Test and debug fairy transactions through RPC";

        public struct Settings
        {
            public long MaxGasInvoke = (long)new BigDecimal(10M, NativeContract.GAS.Decimals).Value;
            public int MaxIteratorResultItems = 1024;
        }

        private static NeoSystem? system;
        private static Settings settings = new();

        protected override void Configure()
        {
            IConfigurationSection config = GetConfiguration();
            settings = new Settings
            {
                MaxGasInvoke = (long)new BigDecimal(config.GetValue("MaxGasInvoke", 10M), NativeContract.GAS.Decimals).Value,
                MaxIteratorResultItems = config.GetValue("MaxIteratorResultItems", 1024),
            };
        }

        protected override void OnSystemLoaded(NeoSystem System)
        {
            system = System;
            if (RpcServerPlugin.RegisterMethods(this, system.Settings.Network))
                return;
            Plugin? incarnatedRpcServerPlugin = Plugins.Find(p => p.GetType().Name == nameof(RpcServerPlugin));
            if (incarnatedRpcServerPlugin != null)
            {
                if (incarnatedRpcServerPlugin.PluginHooks.TryGetValue(system.Settings.Network, out object handlerList))
                    ((List<object>)handlerList).Add(this);
                else
                    incarnatedRpcServerPlugin.PluginHooks[system.Settings.Network] = new List<object> { this };
            }
        }
    }
}
