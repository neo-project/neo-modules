using Akka.Actor;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.IO.Json;
using System;
using System.IO;

namespace Neo.Plugins
{
    public class ImportBlocks : Plugin
    {
        private IActorRef _blockImporter;

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        private bool OnExport(string[] args)
        {
            if (args.Length < 2) return false;
            if (!string.Equals(args[1], "block", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(args[1], "blocks", StringComparison.OrdinalIgnoreCase))
                return false;
            if (args.Length >= 3 && uint.TryParse(args[2], out uint start))
            {
                if (start > Blockchain.Singleton.Height)
                    return true;
                uint count = args.Length >= 4 ? uint.Parse(args[3]) : uint.MaxValue;
                count = Math.Min(count, Blockchain.Singleton.Height - start + 1);
                uint end = start + count - 1;

                string path = $"chain.{start}.acc";
                if (Settings.Default.PersistTXState)
                    path += ".v2";
                        
                using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    if (fs.Length > 0)
                    {
                        fs.Seek(sizeof(uint), SeekOrigin.Begin);
                        byte[] buffer = new byte[sizeof(uint)];
                        fs.Read(buffer, 0, buffer.Length);
                        start += BitConverter.ToUInt32(buffer, 0);
                        fs.Seek(sizeof(uint), SeekOrigin.Begin);
                    }
                    else
                    {
                        fs.Write(BitConverter.GetBytes(start), 0, sizeof(uint));
                    }
                    if (start <= end)
                        fs.Write(BitConverter.GetBytes(count), 0, sizeof(uint));
                    fs.Seek(0, SeekOrigin.End);
                    using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
                        for (uint i = start; i <= end; i++)
                        {
                            Block block = snapshot.GetBlock(i);
                            byte[] array = block.ToArray();
                            fs.Write(BitConverter.GetBytes(array.Length), 0, sizeof(int));
                            fs.Write(array, 0, array.Length);

                            if (Settings.Default.PersistTXState)
                                if(!ExportAppLog(block, fs))
                                {
                                    Console.Write("Error while exporting files with AppLog\n");
                                    return false;
                                }
                        
                            Console.SetCursorPosition(0, Console.CursorTop);
                            Console.Write($"[{i}/{end}]");
                        }
                }
            }
            else
            {
                start = 0;
                uint end = Blockchain.Singleton.Height;
                uint count = end - start + 1;
                string path = args.Length >= 3 ? args[2] : "chain.acc";
                if (Settings.Default.PersistTXState)
                    path += ".v2";
                
                using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    if (fs.Length > 0)
                    {
                        byte[] buffer = new byte[sizeof(uint)];
                        fs.Read(buffer, 0, buffer.Length);
                        start = BitConverter.ToUInt32(buffer, 0);
                        fs.Seek(0, SeekOrigin.Begin);
                    }
                    if (start <= end)
                        fs.Write(BitConverter.GetBytes(count), 0, sizeof(uint));
                    fs.Seek(0, SeekOrigin.End);
                    using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
                        for (uint i = start; i <= end; i++)
                        {
                            Block block = snapshot.GetBlock(i);
                            byte[] array = block.ToArray();
                            fs.Write(BitConverter.GetBytes(array.Length), 0, sizeof(int));
                            fs.Write(array, 0, array.Length);

                            if (Settings.Default.PersistTXState)
                                if (!ExportAppLog(block, fs))
                                {
                                    Console.Write("Error while exporting files with AppLog.");
                                    return false;
                                }

                            Console.SetCursorPosition(0, Console.CursorTop);
                            Console.Write($"[{i}/{end}]");
                        }
                }
            }
            Console.WriteLine();
            return true;
        }

        private bool ExportAppLog(Block block, FileStream fs)
        {
            foreach (Transaction tx in block.Transactions)
            {
                if (tx.Type != TransactionType.InvocationTransaction)
                    continue;
                
                JArray _params = new JArray();
                JString txHash = new JString(tx.Hash.ToString());
                Console.Write($"Export tx {txHash}\n");

                _params.Add(txHash);
                JObject getAppLogRequest = new JObject();
                getAppLogRequest["id"] = -1;
                getAppLogRequest["method"] = "getapplicationlog";
                getAppLogRequest["params"] = _params;
                JObject txState = System.RpcServer.ProcessRequest(null, getAppLogRequest);

                Console.Write($"Result is {txState["result"]}\n");
                if (!txState["result"])
                    if ((int)txState["code"].AsNumber() == -100 || (int)txState["code"].AsNumber() == -32602 || (int)txState["code"].AsNumber() == -32600)
                    {
                        Console.Write($"Code is {txState["code"]}\n");
                        return false;
                    }

                JArray appExecuted = (JArray)txState["result"]["executions"];
                uint nExec = (uint)appExecuted.Count;
                fs.Write(BitConverter.GetBytes(nExec), 0, sizeof(uint));
                byte[] vmStates = new byte[nExec];
                for (int k = 0; k < nExec; k++)
                    vmStates[k] = appExecuted[k]["vmstate"].ToString().Contains("FAULT") ? (byte)VM.VMState.FAULT : (byte)VM.VMState.HALT;

                Console.Write($"Exporting states {nExec} = {vmStates}\n");

                fs.Write(vmStates, 0, vmStates.Length);
            }
            return true;
        }

        private bool OnHelp(string[] args)
        {
            if (args.Length < 2) return false;
            if (!string.Equals(args[1], Name, StringComparison.OrdinalIgnoreCase))
                return false;
            Console.Write($"{Name} Commands:\n" + "\texport block[s] <index>\n");
            return true;
        }

        private void OnImportComplete()
        {
            ResumeNodeStartup();
            System.ActorSystem.Stop(_blockImporter);
        }

        protected override void OnPluginsLoaded()
        {
            SuspendNodeStartup();
            _blockImporter = System.ActorSystem.ActorOf(BlockImporter.Props());
            _blockImporter.Tell(new BlockImporter.StartImport { BlockchainActorRef = System.Blockchain, OnComplete = OnImportComplete });
        }

        protected override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args.Length == 0) return false;
            switch (args[0].ToLower())
            {
                case "help":
                    return OnHelp(args);
                case "export":
                    return OnExport(args);
            }
            return false;
        }
    }
}
