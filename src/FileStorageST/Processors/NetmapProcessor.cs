using Neo.FileStorage.Listen;
using Neo.FileStorage.Listen.Event;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Processors
{
    public class NetmapProcessor : IProcessor
    {
        private const string NewEpochNotification = "NewEpoch";
        private readonly UInt160 netmapScriptHash;

        private readonly List<ParserInfo> parsers = new();
        private readonly List<HandlerInfo> handlers = new();

        public NetmapProcessor(UInt160 hash)
        {
            netmapScriptHash = hash;
        }

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
                    ScriptHashValue = netmapScriptHash,
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
                    ScriptHashValue = netmapScriptHash,
                    Type = NewEpochNotification,
                },
                Handler = handler,
            });
        }
    }
}
