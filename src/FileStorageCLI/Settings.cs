using Microsoft.Extensions.Configuration;
using Neo;

namespace FileStorageCLI
{
    public class Settings
    {
        public static Settings Default { get; private set; }
        public string Host;
        public UInt160 FsContractHash;

        private Settings(IConfigurationSection section)
        {
            Host = section.GetValue("Host", "http://192.168.130.71:8080");
            FsContractHash = UInt160.Parse(section.GetSection("FsContractHash").Value);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
