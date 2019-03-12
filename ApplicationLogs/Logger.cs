using Akka.Actor;
using Neo.IO.Data.LevelDB;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.VM;
using System;
using System.Linq;

namespace Neo.Plugins
{
    internal class Logger : UntypedActor
    {
        private readonly DB db;

        public Logger(DB db)
        {
            this.db = db;
            Context.System.EventStream.Subscribe(Self, typeof(Blockchain.ApplicationExecuted));
        }

        protected override void OnReceive(object message)
        {
            if (!(message is Blockchain.ApplicationExecuted appExec)) return;
            JObject json = new JObject();
            json["txid"] = appExec.Transaction.Hash.ToString();
            json["executions"] = appExec.ExecutionResults.Select(p =>
            {
                JObject execution = new JObject();
                execution["trigger"] = p.Trigger;
                execution["contract"] = p.ScriptHash.ToString();
                execution["vmstate"] = p.VMState;
                execution["gas_consumed"] = p.GasConsumed.ToString();
                try
                {
                    execution["stack"] = p.Stack.Select(q => q.ToParameter().ToJson()).ToArray();
                }
                catch (InvalidOperationException)
                {
                    execution["stack"] = "error: recursive reference";
                }
                execution["notifications"] = p.Notifications.Select(q =>
                {
                    JObject notification = new JObject();
                    notification["contract"] = q.ScriptHash.ToString();
                    try
                    {
                        notification["state"] = q.State.ToParameter().ToJson();
                    }
                    catch (InvalidOperationException)
                    {
                        notification["state"] = "error: recursive reference";
                    }
                    return notification;
                }).ToArray();
                return execution;
            }).ToArray();
            db.Put(WriteOptions.Default, appExec.Transaction.Hash.ToArray(), json.ToString());
        }

        public static Props Props(DB db)
        {
            return Akka.Actor.Props.Create(() => new Logger(db));
        }
    }
}
