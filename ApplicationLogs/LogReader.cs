using Microsoft.AspNetCore.Http;
using Neo.IO.Data.LevelDB;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.RPC;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Snapshot = Neo.Persistence.Snapshot;

namespace Neo.Plugins
{
    public class LogReader : Plugin, IRpcPlugin, IPersistencePlugin, ILogPlugin
    {
        private readonly DB db;

        public override string Name => "ApplicationLogs";

        public LogReader()
        {
            db = DB.Open(Path.GetFullPath(Settings.Default.Path), new Options { CreateIfMissing = true });
        }

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }
        
        public void Log(string source, LogLevel level, string message)
        {
            if (!Settings.Default.ConsoleOutput) return;

            var currentColor = new ConsoleColorSet();

            switch (level)
            {
                case LogLevel.Debug: ConsoleColorSet.Debug.Apply(); break;
                case LogLevel.Error: ConsoleColorSet.Error.Apply(); break;
                case LogLevel.Fatal: ConsoleColorSet.Fatal.Apply(); break;
                case LogLevel.Info: ConsoleColorSet.Info.Apply(); break;
                case LogLevel.Warning: ConsoleColorSet.Warning.Apply(); break;
            }

            Console.WriteLine($"[{DateTime.Now.TimeOfDay:hh\\:mm\\:ss\\.fff}] {message}");

            currentColor.Apply();
        }

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (method != "getapplicationlog") return null;
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            if (!db.TryGet(ReadOptions.Default, hash.ToArray(), out Slice value))
                throw new RpcException(-100, "Unknown transaction");
            return JObject.Parse(value.ToString());
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }

        public void OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            WriteBatch writeBatch = new WriteBatch();

            foreach (var appExec in applicationExecutedList)
            {
                JObject json = new JObject();
                json["txid"] = appExec.Transaction.Hash.ToString();
                json["trigger"] = appExec.Trigger;
                json["vmstate"] = appExec.VMState;
                json["gas_consumed"] = appExec.GasConsumed.ToString();
                try
                {
                    json["stack"] = appExec.Stack.Select(q => q.ToParameter().ToJson()).ToArray();
                }
                catch (InvalidOperationException)
                {
                    json["stack"] = "error: recursive reference";
                }
                json["notifications"] = appExec.Notifications.Select(q =>
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
                writeBatch.Put(appExec.Transaction.Hash.ToArray(), json.ToString());
            }
            db.Write(WriteOptions.Default, writeBatch);
        }

        public void OnCommit(Snapshot snapshot)
        {
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex)
        {
            return false;
        }
    }
}
