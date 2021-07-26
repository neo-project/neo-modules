using System;
using System.Collections.Generic;
using Neo.FileStorage.Listen;
using Neo.FileStorage.Listen.Event;

namespace Neo.FileStorage.Storage.Processors
{
    public class NetmapProcessor : IProcessor
    {
        private const string NewEpochNotification = "NewEpoch";
        private readonly UInt160 NetmapScriptHash = Settings.Default.NetmapContractHash;

        private readonly List<ParserInfo> parsers = new();
        private readonly List<HandlerInfo> handlers = new();

        ParserInfo[] IProcessor.ListenerParsers()
        {
            return parsers.ToArray();
        }

        HandlerInfo[] IProcessor.ListenerHandlers()
        {
            return handlers.ToArray();
        }

        HandlerInfo[] IProcessor.TimersHandlers()
        {
            return Array.Empty<HandlerInfo>();
        }

        public void AddEpochParser(Func<VM.Types.Array, ContractEvent> parser)
        {
            parsers.Add(new()
            {
                ScriptHashWithType = new()
                {
                    ScriptHashValue = NetmapScriptHash,
                    Type = NewEpochNotification,
                },
                Parser = parser
            });
        }

        public void AddEpochHandler(Action<ContractEvent> handler)
        {
            handlers.Add(new()
            {
                ScriptHashWithType = new()
                {
                    ScriptHashValue = NetmapScriptHash,
                    Type = NewEpochNotification,
                },
                Handler = handler,
            });
        }
    }
}
