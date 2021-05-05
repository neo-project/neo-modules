using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Akka.Actor;
using Cron.IO;
using Cron.Ledger;
using Cron.Network.P2P.Payloads;
using Cron.Plugins.SyncBlocks.Extensions;

namespace Cron.Plugins.SyncBlocks
{
    public class ImportService : UntypedActor
    {
        private IActorRef _blockchainActorRef;
        private bool _isImporting;
        private Action _doneAction;
        private List<Block> _importBlocks;
        private static int _importChunkSize = 100000;
        
        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new ImportService());
        }
        
        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case PrepareBulkImport import:
                    if (_isImporting) return;
                    _isImporting = true;
                    _blockchainActorRef = import.BlockchainActorRef;
                    _doneAction = import.OnComplete;
                    _importBlocks = GetBlocksFromFiles();
                    Self.Tell(new ProcessBulkImport());
                    break;
                case ProcessBulkImport _:
                    if (_importBlocks != null)
                    {
                        _importBlocks = _importBlocks
                            .Where(x => x.Header.Index >= Blockchain.Singleton.Height)
                            .OrderBy(x => x.Header.Index)
                            .ToList();
                        if (_importBlocks.Any())
                        {
                            var chunk = _importBlocks.Extract(_importChunkSize);
                            Console.WriteLine($"Import next {_importChunkSize} blocks.");
                            _blockchainActorRef.Tell(new Blockchain.Import { Blocks = chunk });
                        }
                        else
                        {
                            Console.WriteLine("Finish import blocks.");
                            _isImporting = false;
                            _doneAction();
                        }
                    }
                    break;
                case Blockchain.ImportCompleted _:
                    if (_importBlocks.Any())
                    {
                        Console.WriteLine("Blocks imported.");
                        Self.Tell(new ProcessBulkImport());
                    }
                    else
                    {
                        Console.WriteLine("Finish import blocks.");
                        _isImporting = false;
                        _doneAction();
                    }
                    break;
            }
        }
        
        private static List<Block> GetBlocksFromFiles()
        {
            var fileMask = "block_chunk_*.acc";
            var path = GetAssemblyDirectory();
            Console.WriteLine($"Root directory: {path}");
            var files = Directory.GetFiles(path, fileMask);
            if (!files.Any())
            {
                Console.WriteLine($"No files with mask {fileMask} into directory {path}");
                return null;
            }
            
            Console.WriteLine($"- Read blocks from files: {DateTime.Now}");
            var items = new List<Block>();
            foreach (var f in files)
            {
                Console.WriteLine($"+-- Start read blocks from file {f}: {DateTime.Now}");
                
                var blocks = GetBlocks(f);
                items.AddRange(blocks);
                
                var newFileName = $"{f}p";
                File.Move(f, newFileName);
                Console.WriteLine($"+-- Finish read blocks from file {f}: {DateTime.Now}");
            }

            Console.WriteLine($"-Finish read blocks from files: {DateTime.Now}");
            return items.OrderBy(x => x.Index).ToList();
        }
        
        private static IEnumerable<Block> GetBlocks(string fileName, bool readStart = false)
        {
            var result = new List<Block>();
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var r = new BinaryReader(fs))
                {
                    var start = readStart ? r.ReadUInt32() : 0;
                    var count = r.ReadUInt32();
                
                    if (count == 0)
                        return result;
                
                    var end = checked(start + count) - 1;
                
                    if (end <= Blockchain.Singleton.Height)
                        return result;

                    var i = 0;
                    for (var height = start; height <= end; height++)
                    {
                        byte[] array = null;
                        try
                        {
                            array = r.ReadBytes(r.ReadInt32());
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                        
                        if (height <= Blockchain.Singleton.Height)
                            continue;
                    
                        var block = array.AsSerializable<Block>();
                        result.Add(block);
                        i++;
                        if (i % 100000 == 0)
                            Console.WriteLine($"Read {i} blocks");
                    }
                }
            }

            return result;
        }
        
        private static string GetAssemblyDirectory()
        {
            return Directory.GetCurrentDirectory();
        }
    }
}