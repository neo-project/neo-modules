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
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            byte[] value = db.Get(ReadOptions.Default, hash.ToArray());
            if (value is null)
                throw new RpcException(-100, "Unknown transaction/blockhash");
            return JObject.Parse(Encoding.UTF8.GetString(value));
        }

        public void OnPersist(StoreView snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            WriteBatch writeBatch = new WriteBatch();

            //processing log for transactions
            foreach (var appExec in applicationExecutedList.Where(p => p.Transaction != null))
            {
                var txJson = new JObject();
                txJson["txid"] = appExec.Transaction.Hash.ToString();
                txJson["trigger"] = appExec.Trigger;
                txJson["vmstate"] = appExec.VMState;
                txJson["gasconsumed"] = appExec.GasConsumed.ToString();
                try
                {
                    txJson["stack"] = appExec.Stack.Select(q => q.ToJson()).ToArray();
                }
                catch (InvalidOperationException)
                {
                    txJson["stack"] = "error: recursive reference";
                }
                txJson["notifications"] = appExec.Notifications.Select(q =>
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
                writeBatch.Put(appExec.Transaction.Hash.ToArray(), Encoding.UTF8.GetBytes(txJson.ToString()));
            }

            //processing log for block
            var blocks = applicationExecutedList.Where(p => p.Transaction == null);
            if (blocks.Count() > 0)
            {
                var blockJson = new JObject();
                var blockHash = snapshot.PersistingBlock.Hash.ToArray();
                blockJson["blockhash"] = blockHash.ToString();
                var executedList = new List<JObject>();
                foreach (var appExec in blocks)
                {
                    JObject trigger = new JObject();
                    trigger["trigger"] = appExec.Trigger;
                    trigger["vmstate"] = appExec.VMState;
                    trigger["gasconsumed"] = appExec.GasConsumed.ToString();
                    try
                    {
                        trigger["stack"] = appExec.Stack.Select(q => q.ToJson()).ToArray();
                    }
                    catch (InvalidOperationException)
                    {
                        trigger["stack"] = "error: recursive reference";
                    }
                    trigger["notifications"] = appExec.Notifications.Select(q =>
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
                    executedList.Add(trigger);
                }
                blockJson["executed"] = executedList.ToArray();
                writeBatch.Put(blockHash, Encoding.UTF8.GetBytes(blockJson.ToString()));
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
