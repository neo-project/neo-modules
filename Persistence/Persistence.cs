using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Persistence;
using Neo.IO.Caching;
using Neo.IO.Json;
using Neo.Ledger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;



namespace Neo.Plugins
{
    
    public class PersistencePlugin : Plugin, IPersistencePlugin
    {
        
        public void OnPersist(Snapshot snapshot)
        {
            uint blockIndex = snapshot.Height;
            string dirPath = "./Storage";
            Directory.CreateDirectory(dirPath);
            string path = $"{HandlePaths(dirPath, blockIndex)}/dump-block-{blockIndex.ToString()}.json";

            JArray array = new JArray();
 
            foreach (DataCache<StorageKey, StorageItem>.Trackable trackable in snapshot.Storages.GetChangeSet())
            {
                JObject state = new JObject();

                switch (trackable.State)
                {
                    case TrackState.Added:
                        state["state"] = "Added";
                        state["key"] = trackable.Key.ToString();
                        state["value"] = trackable.Item.ToJson();
                        // Here we have a new trackable.Key and trackable.Item
                        break;
                    case TrackState.Changed:
                        state["state"] = "Changed";
                        state["key"] = trackable.Key.ToString();
                        state["value"] = trackable.Item.ToJson();
                        break;
                    case TrackState.Deleted:
                        state["state"] = "Deleted";
                        state["key"] = trackable.Key.ToString();
                        break;
                }
                array.Add(state);
            }

            Settings.Default.BlockStorageCache = Settings.Default.BlockStorageCache + "{\"block\":" + blockIndex.ToString() + ",\"size\":" + array.Count.ToString() + ",\"storage\":\n";
            Settings.Default.BlockStorageCache = Settings.Default.BlockStorageCache + array.ToString() + "},\n";

            if ((blockIndex % Settings.Default.BlockCacheSize == 0) || (blockIndex > Settings.Default.HeightToStartRealTimeSyncing))
            {
                Settings.Default.BlockStorageCache += "]";
                File.WriteAllText(path, Settings.Default.BlockStorageCache);
                Settings.Default.BlockStorageCache = "[";
	        }

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
