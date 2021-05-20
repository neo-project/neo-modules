using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Akka.Actor;
using Cron.Cryptography.ECC;
using Cron.Interface;
using Cron.IO;
using Cron.IO.Json;
using Cron.Ledger;
using Cron.Network.P2P.Payloads;
using Cron.Plugins.SyncBlocks.Model;

namespace Cron.Plugins.SyncBlocks
{
    public class ImportService : UntypedActor
    {
        private IActorRef _blockchainActorRef;
        private bool _isImporting;
        private Action _doneAction;
        private List<BlockFile> _blockFiles;
        private static ICronLogger _logger;
        
        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new ImportService());
        }
        
        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case PrepareBulkImport prepare:
                    if (_isImporting) return;
                    _isImporting = true;
                    _logger = prepare.Logger;
                    _blockchainActorRef = prepare.BlockchainActorRef;
                    _doneAction = prepare.OnComplete;
                    ImportAssetsFromFile();
                    _blockFiles = GetBlocksFiles();
                    Self.Tell(new ProcessNextBlockFile());
                    break;
                case ProcessNextBlockFile _:
                    ImportBlockFromFiles();
                    break;
                case ProcessImportBlocks processImport:
                    if (processImport.Blocks != null && processImport.Blocks.Any())
                    {
                        _logger?.Info($"Import next {processImport.Blocks.Count} blocks.");
                        _blockchainActorRef.Tell(new Blockchain.Import { Blocks = processImport.Blocks });
                    }
                    else
                    {
                        Self.Tell(new ProcessNextBlockFile());
                    }
                    break;
                case BulkImportCompleted _:
                    _logger?.Info("Finish import blocks.");
                    _isImporting = false;
                    _doneAction();
                    break;
                case Blockchain.ImportCompleted _:
                    if (_blockFiles.Any(x => !x.Processed))
                    {
                        _logger?.Info("Blocks imported.");
                        Self.Tell(new ProcessNextBlockFile());
                    }
                    else
                    {
                        _logger?.Info("Finish import blocks.");
                        _isImporting = false;
                        _doneAction();
                    }
                    break;
            }
        }

        private void ImportAssetsFromFile()
        {
            const string fileName = "assets.acc";
            var importPath = Tools.GetImportDirectory();
            if (!Directory.Exists(importPath))
            {
                _logger?.Error($"Directory {importPath} does not exist.");
                return;
            }
            _logger?.Info($"Scan directory: {importPath}");
            var filePath = $"{importPath}{fileName}";
            var fileExist = File.Exists(filePath);
            if (!fileExist)
            {
                _logger?.Error($"No file {fileName} into directory {importPath}");
                return;
            }
            _logger?.Info($"-Import assets from file: {DateTime.Now}");
            var fileContent = File.ReadAllText(filePath);
            var jObject = JObject.Parse(fileContent);
            var assets = new List<AssetState>();
            foreach (var jAsset in (JArray)jObject)
            {
                var assetTypeValue = jAsset["type"].AsString();
                if (Enum.TryParse(assetTypeValue, true, out AssetType assetType))
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
            ImportAssets(assets);
            _logger?.Info($"-Finish import assets from files: {DateTime.Now}");
        }
        
        private void ImportBlockFromFiles()
        {
            if (_blockFiles == null || !_blockFiles.Any())
            {
                Self.Tell(new BulkImportCompleted());
                return;
            }

            if (_blockFiles.All(x => x.Processed))
            {
                Self.Tell(new BulkImportCompleted());
                return;
            }

            var blockFile = _blockFiles
                .Where(x => !x.Processed)
                .OrderBy(x => x.Index)
                .FirstOrDefault();

            if (blockFile != null)
            {
                _logger?.Info($"- Import blocks from file {blockFile.FilePath}: {DateTime.Now}");
                var blocks = GetBlocks(blockFile.FilePath);
                blocks = blocks.Where(x => x.Header.Index >= Blockchain.Singleton.Height)
                    .OrderBy(x => x.Header.Index)
                    .ToList();
                blockFile.Processed = true;
                var newFileName = $"{blockFile.FilePath}p";
                File.Move(blockFile.FilePath, newFileName);
                Self.Tell(new ProcessImportBlocks { Blocks = blocks });
            }
            else
            {
                Self.Tell(new BulkImportCompleted());
            }
        }
        
        private static List<BlockFile> GetBlocksFiles()
        {
            const string fileMask = "block_chunk_*.acc";
            var importPath = Tools.GetImportDirectory();
            
            if (!Directory.Exists(importPath))
            {
                _logger?.Error($"Directory {importPath} does not exist.");
                return null;
            }
            
            var files = Directory.GetFiles(importPath, fileMask);
            if (!files.Any())
            {
                _logger?.Error($"No files with mask {fileMask} into directory {importPath}");
                return null;
            }

            return files
                .Select(BlockFile.Create)
                .OrderBy(x => x.Index)
                .ToList();
        }
        
        private static List<Block> GetBlocks(string fileName)
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
                            _logger?.Error(e, "Error while read block from file");
                        }
                        
                        var block = array.AsSerializable<Block>();
                        result.Add(block);
                        if (i % 100000 == 0)
                            _logger?.Info($"Read {i} blocks");
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