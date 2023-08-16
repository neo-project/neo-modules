using Microsoft.Extensions.Configuration;
using Neo.Plugins.RestServer.Newtonsoft.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Net;

namespace Neo.Plugins.RestServer
{
    public class RestServerSettings
    {
        #region Settings

        public uint Network { get; init; }
        public IPAddress BindAddress { get; init; }
        public uint Port { get; init; }
        public JsonSerializerSettings JsonSerializerSettings { get; init; }

        #endregion

        #region Static Functions

        public static RestServerSettings Default { get; } = new()
        {
            Network = 860833102u,
            BindAddress = IPAddress.None,
            Port = 10339u,
            JsonSerializerSettings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.None,
                Converters = new JsonConverter[]
                {
                    new StringEnumConverter(),
                    new UInt160JsonConverter(),
                    new UInt256JsonConverter(),
                    new ECPointJsonConverter(),
                    new ReadOnlyMemoryBytesJsonConverter(),
                    new VmArrayJsonConverter(),
                    new VmMapJsonConverter(),
                    new VmStructJsonConverter(),
                    new VmBooleanJsonConverter(),
                    new VmBufferJsonConverter(),
                    new VmByteStringJsonConverter(),
                    new VmIntegerJsonConverter(),
                    new VmNullJsonConverter(),
                    new VmPointerJsonConverter(),
                    new StackItemJsonConverter(),
                },
            },
        };

        public static RestServerSettings Load(IConfigurationSection section) =>
            new()
            {
                Network = section.GetValue(nameof(Network), Default.Network),
                BindAddress = IPAddress.Parse(section.GetSection(nameof(BindAddress)).Value),
                Port = section.GetValue(nameof(Port), Default.Port),
                JsonSerializerSettings = Default.JsonSerializerSettings,
            };

        #endregion

    }
}
