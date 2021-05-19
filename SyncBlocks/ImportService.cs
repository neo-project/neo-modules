using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Akka.Actor;
using Cron.Cryptography.ECC;
using Cron.IO;
using Cron.IO.Json;
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
        private List<AssetState> _importAsset;
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
                    _importAsset = GetAssetsFromFile();
                    _importBlocks = GetBlocksFromFiles();
                    Self.Tell(new ProcessBulkImport());
                    break;
                case ProcessBulkImport _:
                    if (_importAsset != null)
                    {
                        ImportAssets(_importAsset);
                    }
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
                    else
                    {
                        _isImporting = false;
                        _doneAction();
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

        private static List<AssetState> GetAssetsFromFile()
        {
            const string fileName = "assets.acc";
            var importPath = Tools.GetImportDirectory();
            if (!Directory.Exists(importPath))
            {
                Console.WriteLine($"Directory {importPath} does not exist.");
                return null;
            }
            Console.WriteLine($"Scan directory: {importPath}");
            var filePath = $"{importPath}{fileName}";
            var fileExist = File.Exists(filePath);
            if (!fileExist)
            {
                Console.WriteLine($"No file {fileName} into directory {importPath}");
                return null;
            }
            Console.WriteLine($"-Read assets from file: {DateTime.Now}");
            var fileContent = File.ReadAllText(filePath);
            var jObject = JObject.Parse(fileContent);
            var assets = new List<AssetState>();
            foreach (var jAsset in (JArray)jObject)
            {
                var assetTypeValue = jAsset["type"].AsString();
                if (Enum.TryParse(typeof(AssetType), assetTypeValue, true, out var assetType))
                {
                    var asset = new AssetState
                    {
                        AssetId = UInt256.Parse(jAsset["id"].AsString()),
                        AssetType = (AssetType) assetType,
                        Name = jAsset["name"].AsString(),
                        Amount = Fixed8.Parse(jAsset["amount"].AsString()),
                        Available = Fixed8.Parse(jAsset["available"].AsString()),
                        Issuer = UInt160.Parse(jAsset["issuer"].AsString()),
                        Precision = byte.Parse(jAsset["precision"].AsString()),
                        Fee = Fixed8.Parse(jAsset["fee"].AsString()),
                        FeeAddress = UInt160.Parse(jAsset["feeaddress"].AsString()),
                        Owner = ECPoint.Parse(jAsset["owner"].AsString(), ECCurve.Secp256r1),
                        Admin = UInt160.Parse(jAsset["admin"].AsString()),
                        Expiration = uint.Parse(jAsset["expiration"].AsString()),
                        IsFrozen = bool.Parse(jAsset["isfrozen"].AsString())
                    };
                    assets.Add(asset);
                }
            }
            var newFileName = $"{importPath}{fileName}p";
            File.Move(filePath, newFileName);
            Console.WriteLine($"-Finish read assets from files: {DateTime.Now}");
            return assets;
        }
        
        private static List<Block> GetBlocksFromFiles()
        {
            const string fileMask = "block_chunk_*.acc";
            var importPath = Tools.GetImportDirectory();
            
            if (!Directory.Exists(importPath))
            {
                Console.WriteLine($"Directory {importPath} does not exist.");
                return null;
            }
            
            var files = Directory.GetFiles(importPath, fileMask);
            if (!files.Any())
            {
                Console.WriteLine($"No files with mask {fileMask} into directory {importPath}");
                return null;
            }
            
            Console.WriteLine($"- Read blocks from files: {DateTime.Now}");
            var items = new List<Block>();
            foreach (var f in files)
            {
                Console.WriteLine($"+-- Start read blocks from file {f}: {DateTime.Now}");
                
                var blocks = GetBlocks(f);
                items.AddRange(blocks);

                var fi = new FileInfo(f);
                var newFileName = $"{importPath}{fi.Name}p";
                File.Move(f, newFileName);
                
                Console.WriteLine($"+-- Finish read blocks from file {f}: {DateTime.Now}");
            }

            Console.WriteLine($"-Finish read blocks from files: {DateTime.Now}");
            return items.OrderBy(x => x.Index).ToList();
        }
        
        private static IEnumerable<Block> GetBlocks(string fileName)
        {
            var result = new List<Block>();
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var r = new BinaryReader(fs))
                {
                    var count = r.ReadUInt32();
                
                    if (count == 0)
                        return result;
                    
                    for (var i = 0; i < count; i++)
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
                        
                        var block = array.AsSerializable<Block>();
                        result.Add(block);
                        if (i % 100000 == 0)
                            Console.WriteLine($"Read {i} blocks");
                    }
                }
            }

            return result;
        }
        
        private static void ImportAssets(IEnumerable<AssetState> assets)
        {
            using (var snapshot = Blockchain.Singleton.Store.GetSnapshot())
            {
                foreach (var asset in assets)
                {
                    var current = snapshot.Assets.Find().Any(x => x.Key == asset.AssetId);
                    if (!current)
                    {
                        snapshot.Assets.Add(asset.AssetId, asset);
                        snapshot.Commit();
                    }
                }
            }
        }
    }
}