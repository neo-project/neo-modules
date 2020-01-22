using Neo.IO;
using Neo.IO.Data.LevelDB;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Persistence;
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

        public LogReader()
        {
            db = DB.Open(GetFullPath(Settings.Default.Path), new Options { CreateIfMissing = true });
            RpcServer.RegisterMethods(this);
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
                throw new RpcException(-100, "Unknown transaction");
            return JObject.Parse(Encoding.UTF8.GetString(value));
        }

        public void OnPersist(StoreView snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            WriteBatch writeBatch = new WriteBatch();

            foreach (var appExec in applicationExecutedList)
            {
                var appLog = new RpcApplicationLog
                {
                    // how to query the logs with null Transaction?
                    TxHash = appExec.Transaction?.Hash,
                    Trigger = appExec.Trigger,
                    VMState = appExec.VMState,
                    GasConsumed = appExec.GasConsumed,
                    Stack = appExec.Stack.ToList(),
                    Notifications = appExec.Notifications.ToList()
                };

                writeBatch.Put((appExec.Transaction?.Hash ?? snapshot.PersistingBlock.Hash).ToArray(), Encoding.UTF8.GetBytes(appLog.ToJson().ToString()));
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
