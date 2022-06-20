using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Neo.FileStorage.Listen.Event;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;

namespace Neo.FileStorage.Listen
{
    /// <summary>
    /// It is a listener for contract events. It will distribute event to the corresponding processor according to the type of event.
    /// The processor must be bound to the listener during initialization, otherwise it will not work.
    /// Currently, it mainly supports four types of processor:BalanceContractProcessor,ContainerContractProcessor,FsContractProcessor and NetMapContractProcessor
    /// </summary>
    public class Listener : UntypedActor
    {
        private readonly Dictionary<ScriptHashWithType, Func<VM.Types.Array, ContractEvent>> parsers = new();
        private readonly Dictionary<ScriptHashWithType, List<Action<ContractEvent>>> handlers = new();
        private readonly List<Action<Block>> blockHandlers = new();
        private readonly string name;
        private bool started;

        public class BindProcessorEvent { public IProcessor Processor; };
        public class BindBlockHandlerEvent { public Action<Block> Handler; };
        public class NewContractEvent { public NotifyEventArgs Notify; };
        public class NewBlockEvent { public Block Block; };
        public class Start { };
        public class Stop { };

        public Listener(string name)
        {
            this.name = name;
        }

        public void ParseAndHandle(NotifyEventArgs notify)
        {
            if (!started) return;
            if (notify.State is null)
            {
                Utility.Log(nameof(Listener), LogLevel.Warning, $"stack item is not an array type, listener={name}, notify={notify.ParseToJson()}");
                return;
            }
            Utility.Log(nameof(Listener), LogLevel.Info, $"listener={name}, event_type={notify.EventName}");
            var keyEvent = new ScriptHashWithType() { Type = notify.EventName, ScriptHashValue = notify.ScriptHash };
            if (!parsers.TryGetValue(keyEvent, out var parser))
            {
                Utility.Log(nameof(Listener), LogLevel.Warning, $"event parser not set, listener={name}, script_hash={notify.ScriptHash}");
                return;
            }
            ContractEvent contractEvent = null;
            try
            {
                contractEvent = parser(notify.State);
            }
            catch (Exception e)
            {
                Utility.Log(nameof(Listener), LogLevel.Warning, $"could not parse notification, error={e}");
                return;
            }
            if (!handlers.TryGetValue(keyEvent, out var handlersArray) || handlersArray.Count == 0)
            {
                Utility.Log(nameof(Listener), LogLevel.Warning, $"handlers for parsed notification event were not registered, listener={name}, event={contractEvent}");
                return;
            }
            foreach (var handler in handlersArray)
            {
                handler(contractEvent);
            }
        }

        public void RegisterHandler(HandlerInfo p)
        {
            if (started) return;
            if (p.Handler is null) return;
            if (!parsers.ContainsKey(p.ScriptHashWithType)) return;
            if (handlers.TryGetValue(p.ScriptHashWithType, out var value))
                value.Add(p.Handler);
            else
                handlers.Add(p.ScriptHashWithType, new List<Action<ContractEvent>>() { p.Handler });
        }

        public void SetParser(ParserInfo p)
        {
            if (p.Parser is null) return;
            if (started) return;
            parsers.TryAdd(p.ScriptHashWithType, p.Parser);
        }

        public void RegisterBlockHandler(Action<Block> blockHandler)
        {
            if (blockHandler is null)
            {
                Utility.Log(nameof(Listener), LogLevel.Warning, $"ignore nil block handler, listener={name}");
                return;
            }
            blockHandlers.Add(blockHandler);
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Start _:
                    OnStart();
                    break;
                case Stop _:
                    OnStop();
                    break;
                case BindProcessorEvent bindMorphProcessor:
                    BindProcessor(bindMorphProcessor.Processor);
                    break;
                case BindBlockHandlerEvent bindBlockHandler:
                    RegisterBlockHandler(bindBlockHandler.Handler);
                    break;
                case NewContractEvent contractEvent:
                    ParseAndHandle(contractEvent.Notify);
                    break;
                case NewBlockEvent blockEvent:
                    if (started)
                    {
                        foreach (var blockHandler in blockHandlers)
                            blockHandler(blockEvent.Block);
                    }
                    break;
                default:
                    break;
            }
        }

        public void OnStart()
        {
            started = true;
        }

        public void OnStop()
        {
            started = false;
        }

        public void BindProcessor(IProcessor processor)
        {
            ParserInfo[] parsers = processor.ListenerParsers();
            HandlerInfo[] handlers = processor.ListenerHandlers();
            foreach (ParserInfo parser in parsers)
                SetParser(parser);
            foreach (HandlerInfo handler in handlers)
                RegisterHandler(handler);
        }

        public static Props Props(string name)
        {
            return Akka.Actor.Props.Create(() => new Listener(name));
        }
    }
}
