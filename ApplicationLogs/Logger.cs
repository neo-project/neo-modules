using Akka.Actor;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.VM;
using System.IO;
using System.Linq;

namespace Neo.Plugins
{
    internal class Logger : UntypedActor
    {
        public Logger(IActorRef blockchain)
        {
            blockchain.Tell(new Blockchain.Register());
        }

        protected override void OnReceive(object message)
        {
            if (message is Blockchain.ApplicationExecuted e)
            {
                JObject json = new JObject();
                json["txid"] = e.Transaction.Hash.ToString();
                json["executions"] = e.ExecutionResults.Select(p =>
                {
                    JObject execution = new JObject();
                    execution["trigger"] = p.Trigger;
                    execution["contract"] = p.ScriptHash.ToString();
                    execution["vmstate"] = p.VMState;
                    execution["gas_consumed"] = p.GasConsumed.ToString();
                    execution["stack"] = p.Stack.Select(q => q.ToParameter().ToJson()).ToArray();
                    execution["notifications"] = p.Notifications.Select(q =>
                    {
                        JObject notification = new JObject();
                        notification["contract"] = q.ScriptHash.ToString();
                        notification["state"] = q.State.ToParameter().ToJson();
                        return notification;
                    }).ToArray();
                    return execution;
                }).ToArray();
                Directory.CreateDirectory(Settings.Default.Path);
                string path = Path.Combine(Settings.Default.Path, $"{e.Transaction.Hash}.json");
                File.WriteAllText(path, json.ToString());
            }
        }

        public static Props Props(IActorRef blockchain)
        {
            return Akka.Actor.Props.Create(() => new Logger(blockchain));
        }
    }
}
