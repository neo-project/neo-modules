using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cron.Interface;
using Cron.Plugins.SyncBlocks.Extensions;
using Cron.IO;
using Cron.IO.Json;
using Cron.Ledger;
using Cron.Network.P2P.Payloads;

namespace Cron.Plugins.SyncBlocks
{
    public class ExportService
    {
        private readonly ICronLogger _logger;
        public ExportService(ICronLogger logger)
        {
            _logger = logger;
        }
        
        public void ExportData()
        {
            _logger.Info($"-Start export: {DateTime.Now}");
            _logger.Info($"--Start export assets: {DateTime.Now}");
            var assetsCache = Blockchain.Singleton.Store.GetAssets().Find();
            const string assetFileName = "assets.acc";
            var exportPath = Tools.GetExportDirectory();

            if (!Directory.Exists(exportPath))
            {
                try
                {
                    Directory.CreateDirectory(exportPath);
                }
                catch (Exception e)
                {
                    _logger.Error(e, $"Can not create directory {exportPath}.");
                    return;
                }
            }
            
            _logger.Info($"+--- Export directory {exportPath}");
            _logger.Info($"+--- Start export assets in file {assetFileName}: {DateTime.Now}");
            var assets = assetsCache.Select(x => x.Value)
                .OrderBy(x => x.AssetType)
                .ThenBy(x => x.Expiration);

            var assetFilePath = $"{exportPath}{assetFileName}";
            SerializeAssets(assetFilePath, assets);
            _logger.Info($"+--- Finish export assets: {DateTime.Now}");
            _logger.Info($"--Finish export assets: {DateTime.Now}");

            _logger.Info($"--Start collect blocks: {DateTime.Now}");
            var dataCache = Blockchain.Singleton.Store.GetBlocks().Find();
            var blockChunks = dataCache.Where(x => x.Value.TrimmedBlock.IsBlock)
                .Select(x => x.Value.TrimmedBlock.GetBlock(Blockchain.Singleton.Store.GetTransactions()))
                .OrderBy(x => x.Header.Index)
                .ChunkBy(1000000);
            
            _logger.Info($"--Finish collect blocks: length = {blockChunks.Sum(x => x.Count)}: {DateTime.Now}");
            _logger.Info($"--Start export blocks: {DateTime.Now}");
            var chunkIndex = 1;
            foreach (var blocks in blockChunks)
            {
                var fileName = $"block_chunk_{chunkIndex}.acc";
                _logger.Info($"+--- Start chunk {chunkIndex} in file {fileName}: {DateTime.Now}");
                var filePath = $"{exportPath}{fileName}";
                BinarySerialize(filePath, blocks);
                _logger.Info($"+--- Finish chunk {chunkIndex} in file {fileName}: {DateTime.Now}");
                chunkIndex++;
            }
            
            _logger.Info($"--Finish export blocks in file: {DateTime.Now}");
            _logger.Info($"-Finish export: {DateTime.Now}");
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

        private static void SerializeAssets(string fileName, IEnumerable<AssetState> assets)
        {
            var array = new JArray(assets.Select(asset =>
            {
                var state = new JObject
                {
                    ["id"] = asset.AssetId.ToString(),
                    ["type"] = asset.AssetType,
                    ["name"] = asset.GetName(),
                    ["amount"] = asset.Amount.ToString(),
                    ["available"] = asset.Available.ToString(),
                    ["issuer"] = asset.Issuer.ToString(),
                    ["precision"] = asset.Precision.ToString(),
                    ["fee"] = asset.Fee.ToString(),
                    ["feeaddress"] = asset.FeeAddress.ToString(),
                    ["owner"] = asset.Owner.ToString(),
                    ["admin"] = asset.Admin.ToString(),
                    ["expiration"] = asset.Expiration.ToString(),
                    ["isfrozen"] = asset.IsFrozen
                };
                return state;
            }));
            File.WriteAllText(fileName, array.ToString());
        }
    }
}