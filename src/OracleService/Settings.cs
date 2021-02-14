using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

namespace Neo.Plugins
{
    class HttpsSettings
    {
        public TimeSpan Timeout { get; }

        public HttpsSettings(IConfigurationSection section)
        {
            Timeout = TimeSpan.FromMilliseconds(section.GetValue("Timeout", 5000));
        }
    }

    class Settings
    {
        public Uri[] Nodes { get; }
        public TimeSpan MaxTaskTimeout { get; }
        public bool AllowPrivateHost { get; }
        public string[] AllowedContentTypes { get; }
        public HttpsSettings Https { get; }
        public uint Active { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            Nodes = section.GetSection("Nodes").GetChildren().Select(p => new Uri(p.Get<string>(), UriKind.Absolute)).ToArray();
            MaxTaskTimeout = TimeSpan.FromMilliseconds(section.GetValue("MaxTaskTimeout", 432000000));
            AllowPrivateHost = section.GetValue("AllowPrivateHost", false);
            AllowedContentTypes = section.GetSection("AllowedContentTypes").GetChildren().Select(p => p.Get<string>()).ToArray();
            Https = new HttpsSettings(section.GetSection("Https"));
            Active = section.GetValue("Active", 5195086u);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
