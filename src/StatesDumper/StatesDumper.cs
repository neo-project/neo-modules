using Neo.ConsoleService;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Plugins
{
    public class StatesDumper : Plugin, IPersistencePlugin
    {
        private readonly JArray bs_cache = new JArray();
        private NeoSystem System;

        public override string Description => "Exports Neo-CLI status data";

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Magic != Settings.Default.Network) return;
            System = system;
        }

        private static void Dump<TKey, TValue>(IEnumerable<(TKey Key, TValue Value)> states)
            where TKey : ISerializable
            where TValue : ISerializable
        {
            const string path = "dump.json";
            JArray array = new JArray(states.Select(p =>
            {
                JObject state = new JObject();
                state["key"] = Convert.ToBase64String(p.Key.ToArray());
                state["value"] = Convert.ToBase64String(p.Value.ToArray());
                return state;
            }));
            File.WriteAllText(path, array.ToString());
            Console.WriteLine($"States ({array.Count}) have been dumped into file {path}");
        }

        /// <summary>
        /// Process "dump storage" command
        /// </summary>
        [ConsoleCommand("dump storage", Category = "Storage", Description = "You can specify the key or use null to get the corresponding information from the storage")]
        private void OnDumpStorage(UInt160 key = null)
        {
            Dump(key != null
                ? System.StoreView.Find(key.ToArray())
                : System.StoreView.Find());
        }

        void IPersistencePlugin.OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Magic != Settings.Default.Network) return;
            if (Settings.Default.PersistAction.HasFlag(PersistActions.StorageChanges))
                OnPersistStorage(snapshot);
        }

        private void OnPersistStorage(DataCache snapshot)
        {
            uint blockIndex = NativeContract.Ledger.CurrentIndex(snapshot);
            if (blockIndex >= Settings.Default.HeightToBegin)
            {
                JArray array = new JArray();

                foreach (var trackable in snapshot.GetChangeSet())
                {
                    JObject state = new JObject();

                    switch (trackable.State)
                    {

                        case TrackState.Added:
                            state["state"] = "Added";
                            state["key"] = Convert.ToBase64String(trackable.Key.ToArray());
                            state["value"] = Convert.ToBase64String(trackable.Item.ToArray());
                            // Here we have a new trackable.Key and trackable.Item
                            break;
                        case TrackState.Changed:
                            state["state"] = "Changed";
                            state["key"] = Convert.ToBase64String(trackable.Key.ToArray());
                            state["value"] = Convert.ToBase64String(trackable.Item.ToArray());
                            break;
                        case TrackState.Deleted:
                            state["state"] = "Deleted";
                            state["key"] = Convert.ToBase64String(trackable.Key.ToArray());
                            break;
                    }
                    array.Add(state);
                }

                JObject bs_item = new JObject();
                bs_item["block"] = blockIndex;
                bs_item["size"] = array.Count;
                bs_item["storage"] = array;
                bs_cache.Add(bs_item);
            }
        }

        void IPersistencePlugin.OnCommit(NeoSystem system, Block block, DataCache snapshot)
        {
            if (Settings.Default.PersistAction.HasFlag(PersistActions.StorageChanges))
                OnCommitStorage(snapshot);
        }

        public void OnCommitStorage(DataCache snapshot)
        {
            uint blockIndex = NativeContract.Ledger.CurrentIndex(snapshot);
            if (bs_cache.Count > 0)
            {
                if ((blockIndex % Settings.Default.BlockCacheSize == 0) || (Settings.Default.HeightToStartRealTimeSyncing != -1 && blockIndex >= Settings.Default.HeightToStartRealTimeSyncing))
                {
                    string dirPath = "./Storage";
                    Directory.CreateDirectory(dirPath);
                    string path = $"{HandlePaths(dirPath, blockIndex)}/dump-block-{blockIndex}.json";

                    File.WriteAllText(path, bs_cache.ToString());
                    bs_cache.Clear();
                }
            }
        }

        bool IPersistencePlugin.ShouldThrowExceptionFromCommit(Exception ex)
        {
            Console.WriteLine($"Error writing States with StatesDumper.{Environment.NewLine}{ex}");
            return true;
        }

        private static string HandlePaths(string dirPath, uint blockIndex)
        {
            //Default Parameter
            uint storagePerFolder = 100000;
            uint folder = (((blockIndex - 1) / storagePerFolder) + 1) * storagePerFolder;
            if (blockIndex == 0)
                folder = 0;
            string dirPathWithBlock = $"{dirPath}/BlockStorage_{folder}";
            Directory.CreateDirectory(dirPathWithBlock);
            return dirPathWithBlock;
        }
    }
}
