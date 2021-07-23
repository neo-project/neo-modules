using Microsoft.Extensions.Configuration;
using Neo;

namespace FileStorageCLI
{
    public class Settings
    {
        public static Settings Default { get; private set; }

        public string host;

        public UInt160 fsContractHash;

        public string uploadPath;
        public string downloadPath;

        private Settings(IConfigurationSection section)
        {
            uploadPath = section.GetValue("uploadPath",@"./upload/");
            downloadPath = section.GetValue("downloadPath", @"./downloadPath/");
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
