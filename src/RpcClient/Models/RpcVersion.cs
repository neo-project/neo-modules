using Neo.IO.Json;

namespace Neo.Network.RPC.Models
{
    public class RpcVersion
    {
        public class RpcProtocol
        {
            public uint Network { get; set; }
            public int ValidatorsCount { get; set; }
            public uint MillisecondsPerBlock { get; set; }
            public uint MaxValidUntilBlockIncrement { get; set; }
            public uint MaxTraceableBlocks { get; set; }
            public byte AddressVersion { get; set; }
            public uint MaxTransactionsPerBlock { get; set; }
            public int MemoryPoolMaxTransactions { get; set; }
            public ulong InitialGasDistribution { get; set; }

            public JObject ToJson()
            {
                JObject json = new();
                json["network"] = Network;
                json["validatorscount"] = ValidatorsCount;
                json["msperblock"] = MillisecondsPerBlock;
                json["maxvaliduntilblockincrement"] = MaxValidUntilBlockIncrement;
                json["maxtraceableblocks"] = MaxTraceableBlocks;
                json["addressversion"] = AddressVersion;
                json["maxtransactionsperblock"] = MaxTransactionsPerBlock;
                json["memorypoolmaxtransactions"] = MemoryPoolMaxTransactions;
                json["initialgasdistribution"] = InitialGasDistribution;
                return json;
            }

            public static RpcProtocol FromJson(JObject json)
            {
                return new()
                {
                    Network = (uint)json["network"].AsNumber(),
                    ValidatorsCount = (int)json["validatorscount"].AsNumber(),
                    MillisecondsPerBlock = (uint)json["msperblock"].AsNumber(),
                    MaxValidUntilBlockIncrement = (uint)json["maxvaliduntilblockincrement"].AsNumber(),
                    MaxTraceableBlocks = (uint)json["maxtraceableblocks"].AsNumber(),
                    AddressVersion = (byte)json["addressversion"].AsNumber(),
                    MaxTransactionsPerBlock = (uint)json["maxtransactionsperblock"].AsNumber(),
                    MemoryPoolMaxTransactions = (int)json["memorypoolmaxtransactions"].AsNumber(),
                    InitialGasDistribution = (ulong)json["initialgasdistribution"].AsNumber(),
                };
            }
        }

        public int TcpPort { get; set; }

        public int WsPort { get; set; }

        public uint Nonce { get; set; }

        public string UserAgent { get; set; }

        public RpcProtocol Protocol { get; set; } = new();

        public JObject ToJson()
        {
            JObject json = new();
            json["network"] = Protocol.Network; // Obsolete
            json["tcpport"] = TcpPort;
            json["wsport"] = WsPort;
            json["nonce"] = Nonce;
            json["useragent"] = UserAgent;
            json["protocol"] = Protocol.ToJson();
            return json;
        }

        public static RpcVersion FromJson(JObject json)
        {
            return new()
            {
                TcpPort = (int)json["tcpport"].AsNumber(),
                WsPort = (int)json["wsport"].AsNumber(),
                Nonce = (uint)json["nonce"].AsNumber(),
                UserAgent = json["useragent"].AsString(),
                Protocol = RpcProtocol.FromJson(json["protocol"])
            };
        }
    }
}
