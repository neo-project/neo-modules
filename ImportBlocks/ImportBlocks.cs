using Akka.Actor;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace Neo.Plugins
{
    public class ImportBlocks : Plugin
    {
        public ImportBlocks()
        {
            OnImport();
        }

        private static bool CheckMaxOnImportHeight(uint currentImportBlockHeight)
        {
            if (Settings.Default.MaxOnImportHeight == 0 || Settings.Default.MaxOnImportHeight >= currentImportBlockHeight)
                return true;
            return false;
        }

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        private static IEnumerable<Block> GetBlocks(Stream stream, bool read_start = false)
        {
            using (BinaryReader r = new BinaryReader(stream))
            {
                uint start = read_start ? r.ReadUInt32() : 0;
                uint count = r.ReadUInt32();
                uint end = start + count - 1;
                if (end <= Blockchain.Singleton.Height) yield break;
                for (uint height = start; height <= end; height++)
                {
                    byte[] array = r.ReadBytes(r.ReadInt32());
                    if (!CheckMaxOnImportHeight(height)) yield break;
                    if (height > Blockchain.Singleton.Height)
                    {
                        Block block = array.AsSerializable<Block>();
                        yield return block;
                    }
                }
            }
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
                            Console.SetCursorPosition(0, Console.CursorTop);
                            Console.Write($"[{i}/{end}]");
                        }
                }
            }
            Console.WriteLine();
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

        private async void OnImport()
        {
            SuspendNodeStartup();
            const string path_acc = "chain.acc";
            if (File.Exists(path_acc))
                using (FileStream fs = new FileStream(path_acc, FileMode.Open, FileAccess.Read, FileShare.Read))
                    await System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                    {
                        Blocks = GetBlocks(fs)
                    });
            const string path_acc_zip = path_acc + ".zip";
            if (File.Exists(path_acc_zip))
                using (FileStream fs = new FileStream(path_acc_zip, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                using (Stream zs = zip.GetEntry(path_acc).Open())
                    await System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                    {
                        Blocks = GetBlocks(zs)
                    });
            var paths = Directory.EnumerateFiles(".", "chain.*.acc", SearchOption.TopDirectoryOnly).Concat(Directory.EnumerateFiles(".", "chain.*.acc.zip", SearchOption.TopDirectoryOnly)).Select(p => new
            {
                FileName = Path.GetFileName(p),
                Start = uint.Parse(Regex.Match(p, @"\d+").Value),
                IsCompressed = p.EndsWith(".zip")
            }).OrderBy(p => p.Start);
            foreach (var path in paths)
            {
                if (path.Start > Blockchain.Singleton.Height + 1) break;
                if (path.IsCompressed)
                    using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                    using (Stream zs = zip.GetEntry(Path.GetFileNameWithoutExtension(path.FileName)).Open())
                        await System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                        {
                            Blocks = GetBlocks(zs, true)
                        });
                else
                    using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        await System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                        {
                            Blocks = GetBlocks(fs, true)
                        });
            }
            ResumeNodeStartup();
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
