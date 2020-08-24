using Akka.Actor;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
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
            bool writeStart;
            uint start, count;
            string path;
            if (args.Length < 2) return false;
            if (string.Equals(args[1], "root", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length >= 3 && uint.TryParse(args[2], out start))
                {
                    if (Blockchain.Singleton.StateHeight < start) return true;
                    if (start < ProtocolSettings.Default.StateRootEnableIndex)
                        start = ProtocolSettings.Default.StateRootEnableIndex;
                    count = args.Length >= 4 ? uint.Parse(args[3]) : uint.MaxValue;
                    count = (uint)Math.Min(count, Blockchain.Singleton.StateHeight - start + 1);
                }
                else
                {
                    start = ProtocolSettings.Default.StateRootEnableIndex;
                    count = (uint)Blockchain.Singleton.StateHeight - start + 1;
                }
                if (count <= 0)
                {
                    Console.WriteLine("nothing to export.");
                    return true;
                }
                path = $"root.{start}.acc";
                WriteStateRoots(start, count, path);
            }
            else if (string.Equals(args[1], "block", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[1], "blocks", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length >= 3 && uint.TryParse(args[2], out start))
                {
                    if (Blockchain.Singleton.Height < start) return true;
                    count = args.Length >= 4 ? uint.Parse(args[3]) : uint.MaxValue;
                    count = Math.Min(count, Blockchain.Singleton.Height - start + 1);
                    path = $"chain.{start}.acc";
                    writeStart = true;
                }
                else
                {
                    start = 0;
                    count = Blockchain.Singleton.Height - start + 1;
                    path = args.Length >= 3 ? args[2] : "chain.acc";
                    writeStart = false;
                }

                WriteBlocks(start, count, path, writeStart);
            }
            else
            {
                return false;
            }

            Console.WriteLine();
            return true;
        }

        private void WriteStateRoots(uint start, uint count, string path)
        {
            uint end = start + count - 1;
            using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                if (fs.Length > 0)
                {
                    byte[] buffer = new byte[sizeof(uint)];
                    fs.Seek(sizeof(uint), SeekOrigin.Begin);
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
                for (uint i = start; i <= end; i++)
                {
                    StateRootState state = Blockchain.Singleton.Store.GetStateRoots().TryGet(i);
                    byte[] array = state.StateRoot.ToArray();
                    fs.Write(BitConverter.GetBytes(array.Length), 0, sizeof(int));
                    fs.Write(array, 0, array.Length);
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write($"[root:{i}/{end}]");
                }
            }
        }

        private void WriteBlocks(uint start, uint count, string path, bool writeStart)
        {
            uint end = start + count - 1;
            using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                if (fs.Length > 0)
                {
                    byte[] buffer = new byte[sizeof(uint)];
                    if (writeStart)
                    {
                        fs.Seek(sizeof(uint), SeekOrigin.Begin);
                        fs.Read(buffer, 0, buffer.Length);
                        start += BitConverter.ToUInt32(buffer, 0);
                        fs.Seek(sizeof(uint), SeekOrigin.Begin);
                    }
                    else
                    {
                        fs.Read(buffer, 0, buffer.Length);
                        start = BitConverter.ToUInt32(buffer, 0);
                        fs.Seek(0, SeekOrigin.Begin);
                    }
                }
                else
                {
                    if (writeStart)
                    {
                        fs.Write(BitConverter.GetBytes(start), 0, sizeof(uint));
                    }
                }
                if (start <= end)
                    fs.Write(BitConverter.GetBytes(count), 0, sizeof(uint));
                fs.Seek(0, SeekOrigin.End);
                for (uint i = start; i <= end; i++)
                {
                    Block block = Blockchain.Singleton.Store.GetBlock(i);
                    byte[] array = block.ToArray();
                    fs.Write(BitConverter.GetBytes(array.Length), 0, sizeof(int));
                    fs.Write(array, 0, array.Length);
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write($"[block:{i}/{end}]");
                }
            }
        }

        private bool OnHelp(string[] args)
        {
            if (args.Length < 2) return false;
            if (!string.Equals(args[1], Name, StringComparison.OrdinalIgnoreCase))
                return false;
            Console.Write($"{Name} Commands:\n" + "\texport block[s] <index>\n");
            Console.Write($"\texport root <index>\n");
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
