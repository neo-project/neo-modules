using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Event;
using System;
using System.Collections.Generic;

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
            throw new NotImplementedException();
        }

        HandlerInfo[] IProcessor.ListenerHandlers()
        {
            throw new NotImplementedException();
        }

        HandlerInfo[] IProcessor.TimersHandlers()
        {
            throw new NotImplementedException();
        }

        public void AddEpochParser(Func<VM.Types.Array, IContractEvent> parser)
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

        public void AddEpochHandler(Action<IContractEvent> handler)
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
