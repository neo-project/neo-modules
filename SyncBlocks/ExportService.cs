using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cron.Plugins.SyncBlocks.Extensions;
using Cron.IO;
using Cron.Ledger;
using Cron.Network.P2P.Payloads;

namespace Cron.Plugins.SyncBlocks
{
    public static class ExportService
    {
        public static void ExportData()
        {
            Console.WriteLine($"-Start collect blocks: {DateTime.Now}");
            var dataCache = Blockchain.Singleton.Store.GetBlocks().Find();
            var blockChunks = dataCache.Where(x => x.Value.TrimmedBlock.IsBlock)
                .Select(x => x.Value.TrimmedBlock.GetBlock(Blockchain.Singleton.Store.GetTransactions()))
                .OrderBy(x => x.Header.Index)
                .ChunkBy(1000000);
            
            Console.WriteLine($"-Finish collect blocks: length = {blockChunks.Sum(x => x.Count)}: {DateTime.Now}");
            Console.WriteLine($"-Start export blocks: {DateTime.Now}");
            var chunkIndex = 1;
            foreach (var blocks in blockChunks)
            {
                var fileName = $"block_chunk_{chunkIndex}.acc";
                Console.WriteLine($"+-- Start chunk {chunkIndex} in file {fileName}: {DateTime.Now}");
                BinarySerialize(fileName, blocks);
                Console.WriteLine($"+-- Finish chunk {chunkIndex} in file {fileName}: {DateTime.Now}");
                chunkIndex++;
            }
            
            Console.WriteLine($"-Finish export blocks in file: {DateTime.Now}");
        }
        
        private static void BinarySerialize(string fileName, ICollection<Block> blocks)
        {
            using (var fs = new FileStream(fileName, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var count = blocks.Count;
                fs.Write(BitConverter.GetBytes(count), 0, sizeof(uint));
                fs.Seek(0, SeekOrigin.End);
                foreach (var x in blocks.OrderBy(x => x.Header.Index))
                {
                    var array = x.ToArray();
                    fs.Write(BitConverter.GetBytes(array.Length), 0, sizeof(int));
                    fs.Write(array, 0, array.Length);
                }
            }
        }
    }
}