using Akka.Actor;
using Neo.Plugins.FSStorage.innerring.processors;
using Neo.Plugins.util;
using Neo.SmartContract;
using System;
using System.Collections.Generic;

namespace Neo.Plugins.FSStorage
{
    /// <summary>
    /// It is a listener for contract events. It will distribute event to the corresponding processor according to the type of event.
    /// The processor must be bound to the listener during initialization, otherwise it will not work.
    /// Currently, it mainly supports four types of processor:BalanceContractProcessor,ContainerContractProcessor,FsContractProcessor and NetMapContractProcessor
    /// </summary>
    public class Listener : UntypedActor
    {
        private Dictionary<ScriptHashWithType, Func<VM.Types.Array, IContractEvent>> parsers;
        private Dictionary<ScriptHashWithType, List<Action<IContractEvent>>> handlers;
        private string name;
        private bool started;

        public class BindProcessorEvent { public IProcessor processor; };
        public class NewContractEvent { public NotifyEventArgs notify; };
        public class Start { };
        public class Stop { };

        public Listener(string name)
        {
            this.name = name;
            parsers = new Dictionary<ScriptHashWithType, Func<VM.Types.Array, IContractEvent>>();
            handlers = new Dictionary<ScriptHashWithType, List<Action<IContractEvent>>>();
        }

        public void ParseAndHandle(NotifyEventArgs notify)
        {
            if (started)
            {
                Neo.Utility.Log(name, LogLevel.Info, string.Format("script hash LE:{0}", notify.ScriptHash.ToString()));
                if (notify.State is null)
                {
                    Neo.Utility.Log(name, LogLevel.Warning, string.Format("stack item is not an array type:{0}", notify.ParseToJson().ToString()));
                }
                Neo.Utility.Log(name, LogLevel.Info, string.Format("event type:{0}", notify.EventName));
                var keyEvent = new ScriptHashWithType() { Type = notify.EventName, ScriptHashValue = notify.ScriptHash };
                if (!parsers.TryGetValue(keyEvent, out var parser))
                {
                    Neo.Utility.Log(name, LogLevel.Warning, string.Format("event parser not set:{0}", notify.ScriptHash.ToString()));
                    return;
                }
                IContractEvent contractEvent = null;
                try
                {
                    contractEvent = parser(notify.State);
                }
                catch (Exception e)
                {
                    Neo.Utility.Log(name, LogLevel.Warning, string.Format("could not parse notification event:{0}", e.Message));
                    return;
                }
                if (!handlers.TryGetValue(keyEvent, out var handlersArray) || handlersArray.Count == 0)
                {
                    Neo.Utility.Log(name, LogLevel.Warning, string.Format("handlers for parsed notification event were not registered:{0}", contractEvent.ToString()));
                    return;
                }
                foreach (var handler in handlersArray)
                {
                    handler(contractEvent);
                }
            }
        }

        public void RegisterHandler(HandlerInfo p)
        {
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("script hash LE", p.ScriptHashWithType.ScriptHashValue.ToString());
            pairs.Add("event type", p.ScriptHashWithType.Type);
            Neo.Utility.Log(name, LogLevel.Info, pairs.ParseToString());

            var handler = p.Handler;
            if (handler is null)
            {
                Neo.Utility.Log(name, LogLevel.Warning, string.Format("ignore nil event handler:{0}", pairs.ParseToString()));
                return;
            }
            if (started)
            {
                Neo.Utility.Log(name, LogLevel.Warning, string.Format("listener has been already started, ignore handler:{0}", pairs.ParseToString()));
                return;
            }
            if (!parsers.TryGetValue(p.ScriptHashWithType, out _))
            {
                Neo.Utility.Log(name, LogLevel.Warning, string.Format("ignore handler of event w/o parser:{0}", pairs.ParseToString()));
                return;
            }
            if (handlers.TryGetValue(p.ScriptHashWithType, out var value))
            {
                value.Add(p.Handler);
            }
            else
            {
                handlers.Add(p.ScriptHashWithType, new List<Action<IContractEvent>>() { p.Handler });
            }
            Neo.Utility.Log(name, LogLevel.Info, string.Format("registered new event handler:{0}", pairs.ParseToString()));
        }

        public void SetParser(ParserInfo p)
        {
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("script hash LE", p.ScriptHashWithType.ScriptHashValue.ToString());
            pairs.Add("event type", p.ScriptHashWithType.Type);
            Neo.Utility.Log(name, LogLevel.Info, pairs.ParseToString());

            if (p.Parser is null)
            {
                Neo.Utility.Log(name, LogLevel.Warning, string.Format("ignore nil event parser:{0}", pairs.ParseToString()));
                return;
            }
            if (started)
            {
                Neo.Utility.Log(name, LogLevel.Warning, string.Format("listener has been already started, ignore parser:{0}", pairs.ParseToString()));
                return;
            }
            if (!parsers.TryGetValue(p.ScriptHashWithType, out _))
                parsers.Add(p.ScriptHashWithType, p.Parser);
            Neo.Utility.Log(name, LogLevel.Info, string.Format("registered new event parser:{0}", pairs.ParseToString()));
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
                case NewContractEvent contractEvent:
                    ParseAndHandle(contractEvent.notify);
                    break;
                case BindProcessorEvent bindMorphProcessor:
                    BindProcessor(bindMorphProcessor.processor);
                    break;
                default:
                    break;
            }
        }

        public void OnStart()
        {
            if (!started)
            {
                started = !started;
            }
        }

        public void OnStop()
        {
            if (started)
            {
                started = !started;
            }
        }

        public void BindProcessor(IProcessor processor)
        {
            ParserInfo[] parsers = processor.ListenerParsers();
            HandlerInfo[] handlers = processor.ListenerHandlers();
            foreach (ParserInfo parser in parsers)
            {
                SetParser(parser);
            }
            foreach (HandlerInfo handler in handlers)
            {
                RegisterHandler(handler);
            }
        }

        public static Props Props(string name)
        {
            return Akka.Actor.Props.Create(() => new Listener(name));
        }
    }
}
