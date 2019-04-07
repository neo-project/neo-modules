using Akka.Actor;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace Neo.Plugins
{
    public class BlockImporter : UntypedActor
    {
        public class StartImport { public IActorRef BlockchainActorRef; public Action OnComplete; }

        private const int BlocksPerBatch = 10;
        private IActorRef _blockchainActorRef;
        private bool isImporting;
        private IEnumerator<Block> blocksBeingImported;
        private Action _doneAction;

        private static bool CheckMaxOnImportHeight(uint currentImportBlockHeight)
        {
            if (Settings.Default.MaxOnImportHeight == 0 || Settings.Default.MaxOnImportHeight >= currentImportBlockHeight)
                return true;
            return false;
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

        private IEnumerable<Block> GetBlocksFromFile()
        {
            const string pathAcc = "chain.acc";
            if (File.Exists(pathAcc))
                using (FileStream fs = new FileStream(pathAcc, FileMode.Open, FileAccess.Read, FileShare.Read))
                    foreach (var block in GetBlocks(fs))
                        yield return block;

            const string pathAccZip = pathAcc + ".zip";
            if (File.Exists(pathAccZip))
                using (FileStream fs = new FileStream(pathAccZip, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                using (Stream zs = zip.GetEntry(pathAcc).Open())
                    foreach (var block in GetBlocks(zs))
                        yield return block;

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
                        foreach (var block in GetBlocks(zs, true))
                            yield return block;
                else
                    using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        foreach (var block in GetBlocks(fs, true))
                            yield return block;
            }
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case StartImport startImport:
                    if (isImporting) return;
                    isImporting = true;
                    _blockchainActorRef = startImport.BlockchainActorRef;
                    _doneAction = startImport.OnComplete;
                    blocksBeingImported = GetBlocksFromFile().GetEnumerator();
                    // Start the first import
                    Self.Tell(new Blockchain.ImportCompleted());
                    break;
                case Blockchain.ImportCompleted _:
                    // Import the next batch
                    List<Block> blocksToImport = new List<Block>();
                    for (int i = 0; i < BlocksPerBatch; i++)
                    {
                        if (!blocksBeingImported.MoveNext())
                            break;
                        blocksToImport.Add(blocksBeingImported.Current);
                    }
                    if (blocksToImport.Count > 0)
                        _blockchainActorRef.Tell(new Blockchain.Import { Blocks = blocksToImport });
                    else
                    {
                        blocksBeingImported.Dispose();
                        _doneAction();
                    }
                    break;
            }
        }

        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new BlockImporter());
        }
    }
}
