using Akka.Actor;
using Neo.SmartContract;
using System.Linq;
using Neo.Network.RPC;
using Neo.IO;
using Neo.VM;
using Neo.IO.Json;

namespace Neo.Plugins.FSStorage.innerring
{
    /// <summary>
    /// InnerRingSender is a porter.It is responsible for sending events from mainnet to ir nodes.
    /// </summary>
    public class InnerRingSender : UntypedActor
    {
        public class MainContractEvent { public NotifyEventArgs notify; };

        private RpcClient[] clients;

        public InnerRingSender()
        {
            this.clients = Settings.Default.Urls.Select(p=>new RpcClient(p)).ToArray();
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case MainContractEvent mainEvent:
                    OnMainContractEvent(mainEvent.notify);
                    break;
                default:
                    break;
            }
        }

        private void OnMainContractEvent(NotifyEventArgs notify)
        {
            var container = notify.ScriptContainer.ToArray().ToHexString();
            var scriptHash = notify.ScriptHash.ToArray().ToHexString();
            var eventName = notify.EventName;
            var enumerator = notify.State.GetEnumerator();
            var state = new JArray();
            while (enumerator.MoveNext()) {
                state.Add(enumerator.Current.ToJson());
            }
            foreach(var client in clients) {
                try
                {
                    var result = client.RpcSendAsync("receivemainnetevent", container, scriptHash, eventName, state).Result;
                }
                catch
                {
                    Neo.Utility.Log("NeoFS rpc", LogLevel.Warning, "invoke rpc fail");
                }
            }
        }

        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new InnerRingSender());
        }
    }
}
