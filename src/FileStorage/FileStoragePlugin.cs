using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using System.Collections.Generic;

namespace Neo.FileStorage
{
    /// <summary>
    /// The entrance of the FSNode program.
    /// Built-in an innering service to process notification events related to FS when the block is persisted.
    /// </summary>
    public class FileStoragePlugin : Plugin, IPersistencePlugin
    {
        public override string Name => "FileStorageService";
        public override string Description => "Provide distributed file storage service";

        public NeoSystem MainSystem;
        public NeoSystem SideSystem;
        public InnerRingService InnerRingService;
        public StorageService StorageService;

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
                SideSystem = new(ProtocolSettings.Load(Settings.Default.SideChainConfigPath), Settings.Default.SideChainStorageEngine, "SideChain");
            }
            else if (system.Settings.Network == Settings.Default.SideNetwork)
            {
                if (Settings.Default.StartInnerRing) InnerRingService = new(MainSystem, system);
                if (Settings.Default.StartStorage) StorageService = new(system);
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
                StorageService.OnSidePersisted(block, snapshot, applicationExecutedList);
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
