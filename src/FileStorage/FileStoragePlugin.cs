using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Akka.Actor;
using Neo.ConsoleService;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract.Native;
using Neo.Wallets;

namespace Neo.FileStorage
{
    /// <summary>
    /// The entrance of the FSNode program.
    /// Built-in an innering service to process notification events related to FS when the block is persisted.
    /// </summary>
    public partial class FileStoragePlugin : Plugin, IPersistencePlugin
    {
        public event EventHandler<Wallet> WalletChanged;
        public const string ChainDataFileName = "chain.side.acc";
        public override string Name => "FileStorageService";
        public override string Description => "Provide distributed file storage service";

        public NeoSystem MainSystem;
        public NeoSystem SideSystem;
        public InnerRingService InnerRingService;
        public StorageService StorageService;
        private IWalletProvider walletProvider;

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (!Settings.Default.StartInnerRing && !Settings.Default.StartStorage) return;
            if (system.Settings.Network == Settings.Default.MainNetwork)
            {
                MainSystem = system;
                SideSystem = StartSideChain().Result;
                MainSystem.ServiceAdded += NeoSystem_ServiceAdded;
            }
        }

        public void OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Network == Settings.Default.MainNetwork)
            {
                InnerRingService.OnPersisted(block, snapshot, applicationExecutedList, true);
            }
            else if (system.Settings.Network == Settings.Default.SideNetwork)
            {
                InnerRingService.OnPersisted(block, snapshot, applicationExecutedList, false);
                StorageService.OnPersisted(block, snapshot, applicationExecutedList);
            }
        }

        private void NeoSystem_ServiceAdded(object sender, object service)
        {
            if (service is IWalletProvider)
            {
                walletProvider = service as IWalletProvider;
                MainSystem.ServiceAdded -= NeoSystem_ServiceAdded;
                if (Settings.Default.StartInnerRing || Settings.Default.StartStorage)
                {
                    walletProvider.WalletChanged += WalletProvider_WalletChanged;
                }
            }
        }

        private void WalletProvider_WalletChanged(object sender, Wallet wallet)
        {
            walletProvider.WalletChanged -= WalletProvider_WalletChanged;
            Start(wallet);
        }

        private void Start(Wallet wallet)
        {
            if (Settings.Default.StartInnerRing) InnerRingService = new(wallet, MainSystem, SideSystem);
            if (Settings.Default.StartStorage) StorageService = new(wallet, SideSystem);
        }

        private async Task<NeoSystem> StartSideChain()
        {
            SideChainSettings side_chain_settings = SideChainSettings.Load(Settings.Default.SideChainConfigPath);
            ProtocolSettings protocol_settings = ProtocolSettings.Load(Settings.Default.SideChainConfigPath);
            NeoSystem side_chain = new(protocol_settings, side_chain_settings.Storage.Engine, side_chain_settings.Storage.Path);
            using (IEnumerator<Block> blocksBeingImported = GetBlocksFromFile(side_chain).GetEnumerator())
            {
                while (true)
                {
                    List<Block> blocksToImport = new();
                    for (int i = 0; i < 10; i++)
                    {
                        if (!blocksBeingImported.MoveNext()) break;
                        blocksToImport.Add(blocksBeingImported.Current);
                    }
                    if (blocksToImport.Count == 0) break;
                    await side_chain.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                    {
                        Blocks = blocksToImport,
                        Verify = side_chain_settings.VerifyImport
                    });
                    if (side_chain is null) throw new InvalidOperationException();
                }
            }
            side_chain.StartNode(new ChannelsConfig
            {
                Tcp = new IPEndPoint(IPAddress.Any, side_chain_settings.P2P.Port),
                WebSocket = new IPEndPoint(IPAddress.Any, side_chain_settings.P2P.WsPort),
                MinDesiredConnections = side_chain_settings.P2P.MinDesiredConnections,
                MaxConnections = side_chain_settings.P2P.MaxConnections,
                MaxConnectionsPerAddress = side_chain_settings.P2P.MaxConnectionsPerAddress
            });
            return side_chain;
        }

        private IEnumerable<Block> GetBlocksFromFile(NeoSystem system)
        {
            string pathAcc = ChainDataFileName;
            if (File.Exists(pathAcc))
                using (FileStream fs = new(pathAcc, FileMode.Open, FileAccess.Read, FileShare.Read))
                    foreach (var block in GetBlocks(system, fs))
                        yield return block;

            string pathAccZip = pathAcc + ".zip";
            if (File.Exists(pathAccZip))
                using (FileStream fs = new(pathAccZip, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive zip = new(fs, ZipArchiveMode.Read))
                using (Stream zs = zip.GetEntry(pathAcc).Open())
                    foreach (var block in GetBlocks(system, zs))
                        yield return block;

            var paths = Directory.EnumerateFiles(".", "chain.*.acc", SearchOption.TopDirectoryOnly).Concat(Directory.EnumerateFiles(".", "chain.*.acc.zip", SearchOption.TopDirectoryOnly)).Select(p => new
            {
                FileName = System.IO.Path.GetFileName(p),
                Start = uint.Parse(Regex.Match(p, @"\d+").Value),
                IsCompressed = p.EndsWith(".zip")
            }).OrderBy(p => p.Start);

            uint height = NativeContract.Ledger.CurrentIndex(system.StoreView);
            foreach (var path in paths)
            {
                if (path.Start > height + 1) break;
                if (path.IsCompressed)
                    using (FileStream fs = new(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (ZipArchive zip = new(fs, ZipArchiveMode.Read))
                    using (Stream zs = zip.GetEntry(System.IO.Path.GetFileNameWithoutExtension(path.FileName)).Open())
                        foreach (var block in GetBlocks(system, zs, true))
                            yield return block;
                else
                    using (FileStream fs = new(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        foreach (var block in GetBlocks(system, fs, true))
                            yield return block;
            }
        }

        private IEnumerable<Block> GetBlocks(NeoSystem system, Stream stream, bool read_start = false)
        {
            using BinaryReader r = new(stream);
            uint start = read_start ? r.ReadUInt32() : 0;
            uint count = r.ReadUInt32();
            uint end = start + count - 1;
            uint currentHeight = NativeContract.Ledger.CurrentIndex(system.StoreView);
            if (end <= currentHeight) yield break;
            for (uint height = start; height <= end; height++)
            {
                var size = r.ReadInt32();
                if (size > Message.PayloadMaxSize)
                    throw new ArgumentException($"Block {height} exceeds the maximum allowed size");

                byte[] array = r.ReadBytes(size);
                if (height > currentHeight)
                {
                    Block block = array.AsSerializable<Block>();
                    yield return block;
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            InnerRingService?.Dispose();
            StorageService?.Dispose();
        }
    }
}
