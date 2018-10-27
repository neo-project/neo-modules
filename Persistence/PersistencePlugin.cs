using Neo.Consensus;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Plugins
{
    public class PersistencePlugin : Plugin, IPersistencePlugin
    {
        public void OnPersist(Snapshot snapshot)
        {
            string dirPath = "./Storage";
            Directory.CreateDirectory(dirPath);
            string path = $"{HandlePaths(dirPath, blockIndex)}/dump-block-{blockIndex.ToString()}.json";

            JArray array = new JArray();
            foreach (Trackable trackable in snapshot.Storages.GetChangeSet())
            {
                JObject state = new JObject();
                switch (trackable.State)
                {
                    case TrackState.Added:
                        state["state"] = "Added";
                        state["key"] = trackable.Key;
                        state["value"] = trackable.Item;
                        // Here we have a new trackable.Key and trackable.Item
                        break;
                    case TrackState.Changed:
                        state["state"] = "Changed";
                        state["key"] = trackable.Key;
                        state["value"] = trackable.Item;
                        break;
                    case TrackState.Deleted:
                        state["state"] = "Deleted";
                        state["key"] = trackable.Key;
                        break;
                }
                array.Add(state);
            }

            BlockStorageCache = BlockStorageCache + "{\"block\":" + blockIndex.ToString() + ",\"size\":" + array.Count.ToString() + ",\"storage\":\n";
            BlockStorageCache = BlockStorageCache + array.ToString() + "},\n";

            if ((blockIndex % BlockCacheSize == 0) || (blockIndex > HeightToRealTimeSyncing))
            {
                BlockStorageCache += "]";
                File.WriteAllText(path, BlockStorageCache);
                BlockStorageCache = "[";
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
