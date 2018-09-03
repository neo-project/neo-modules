using Akka.Actor;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public class ImportBlocks : Plugin
    {
        public ImportBlocks()
        {
            Task.Run(() =>
            {
                const string path_acc = "chain.acc";
                if (File.Exists(path_acc))
                    using (FileStream fs = new FileStream(path_acc, FileMode.Open, FileAccess.Read, FileShare.Read))
                        System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                        {
                            Blocks = GetBlocks(fs)
                        }).Wait();
                const string path_acc_zip = path_acc + ".zip";
                if (File.Exists(path_acc_zip))
                    using (FileStream fs = new FileStream(path_acc_zip, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                    using (Stream zs = zip.GetEntry(path_acc).Open())
                        System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                        {
                            Blocks = GetBlocks(zs)
                        }).Wait();
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
                            System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                            {
                                Blocks = GetBlocks(zs, true)
                            }).Wait();
                    else
                        using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                            System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                            {
                                Blocks = GetBlocks(fs, true)
                            }).Wait();
                }
            });
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

                    if (height > Blockchain.Singleton.Height && CheckMaxOnImportHeight(height))
                    {
                        Block block = array.AsSerializable<Block>();
                        yield return block;
                    }
                }
            }
        }

        private static bool CheckMaxOnImportHeight(uint currentImportBlockHeight)
        {
            if (Settings.Default.MaxOnImportHeight == 0 || Settings.Default.MaxOnImportHeight >= currentImportBlockHeight)
                return true;
            return false;
        }
    }
}
