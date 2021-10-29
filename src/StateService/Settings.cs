using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.StateService
{
    internal class Settings
    {
        public string Path { get; }
        public bool FullState { get; }
        public uint Network { get; }
        public bool AutoVerify { get; }
        public int MaxFindResultItems { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            Path = section.GetValue("Path", "Data_MPT_{0}");
            FullState = section.GetValue("FullState", false);
            Network = section.GetValue("Network", 5195086u);
            AutoVerify = section.GetValue("AutoVerify", false);
            MaxFindResultItems = section.GetValue("MaxFindResultItems", 100);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
