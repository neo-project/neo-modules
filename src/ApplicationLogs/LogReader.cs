using Neo.IO;
using Neo.IO.Data.LevelDB;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static System.IO.Path;

namespace Neo.Plugins
{
    public class LogReader : Plugin, IPersistencePlugin
    {
        private readonly DB db;

        public override string Name => "ApplicationLogs";

        public override string Description => "Synchronizes the smart contract log with the NativeContract log (Notify)";

        public LogReader()
        {
            db = DB.Open(GetFullPath(Settings.Default.Path), new Options { CreateIfMissing = true });
            RpcServerPlugin.RegisterMethods(this);
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        [RpcMethod]
        public JObject GetApplicationLog(JArray _params)
        {
            byte[] hash = _params[0].AsString().HexToBytes();
            byte[] value = db.Get(ReadOptions.Default, hash);
            if (value is null)
                foreach (var tpye in Enum.GetValues(typeof(TriggerType)))
                    value = db.Get(ReadOptions.Default, hash.Concat(new byte[] { (byte)tpye }).ToArray());
            else
                return JObject.Parse(Encoding.UTF8.GetString(value));
            if (value is null)
                throw new RpcException(-100, "Unknown transaction or blockhash");
            else
                return JObject.Parse(Encoding.UTF8.GetString(value));
        }

        public void OnPersist(StoreView snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            WriteBatch writeBatch = new WriteBatch();

            foreach (var appExec in applicationExecutedList)
            {
                JObject json = new JObject();
                json["txid"] = appExec.Transaction?.Hash.ToString();
                json["trigger"] = appExec.Trigger;
                json["vmstate"] = appExec.VMState;
                json["gasconsumed"] = appExec.GasConsumed.ToString();
                try
                {
                    json["stack"] = appExec.Stack.Select(q => q.ToJson()).ToArray();
                }
                catch (InvalidOperationException)
                {
                    json["stack"] = "error: recursive reference";
                }
                json["notifications"] = appExec.Notifications.Select(q =>
                {
                    JObject notification = new JObject();
                    notification["contract"] = q.ScriptHash.ToString();
                    notification["eventname"] = q.EventName;
                    try
                    {
                        notification["state"] = q.State.ToJson();
                    }
                    catch (InvalidOperationException)
                    {
                        notification["state"] = "error: recursive reference";
                    }
                    return notification;
                }).ToArray();
                writeBatch.Put(appExec.Transaction?.Hash.ToArray() ??
                    snapshot.PersistingBlock.Hash.ToArray().Concat(new byte[] { (byte)appExec.Trigger }).ToArray(),
                    Encoding.UTF8.GetBytes(json.ToString()));
            }
            db.Write(WriteOptions.Default, writeBatch);
        }

        public void OnCommit(StoreView snapshot)
        {
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex)
        {
            return false;
        }
    }
}
