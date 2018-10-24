using Neo.IO;
using Neo.IO.Caching;
using Neo.IO.Json;
using Neo.Ledger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Plugins
{
    public class StatesDumper : Plugin
    {
        protected override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args.Length < 2) return false;
            if (args[0] != "dump") return false;
            switch (args[1])
            {
                case "storage":
                    Dump(args.Length >= 3
                        ? Blockchain.Singleton.Store.GetStorages().Find(UInt160.Parse(args[2]).ToArray())
                        : Blockchain.Singleton.Store.GetStorages().Find());
                    return true;
                case "blockstorage":
                    DumpInBlock(args.Length >= 3
                        ? Blockchain.Singleton.Store.GetStorages().Find(UInt160.Parse(args[2]).ToArray())
                        : Blockchain.Singleton.Store.GetStorages().Find());
                    return true;
                default:
                    return false;
            }
        }

        private static void Dump<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> states)
            where TKey : ISerializable
            where TValue : ISerializable
        {
            const string path = "dump.json";
            JArray array = new JArray(states.Select(p =>
            {
                JObject state = new JObject();
                state["key"] = p.Key.ToArray().ToHexString();
                state["value"] = p.Value.ToArray().ToHexString();
                return state;
            }));
            File.WriteAllText(path, array.ToString());
            Console.WriteLine($"States have been dumped into file {path}");
        }

        private static void DumpInBlock<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> states)
            where TKey : ISerializable
            where TValue : ISerializable
        {
            string path = $"dump-block-{Blockchain.Singleton.Height.ToString()}.json";
            JArray array = new JArray(states.Where(p => 
            {
                byte[] src = p.Value.ToArray();
                StorageItem si = new StorageItem();
                using (MemoryStream stream = new MemoryStream(src)) {
                  using (BinaryReader reader = new BinaryReader(stream)) {
                    si.Deserialize(reader);
                  }
                }
                uint h = si.Height;
                return h == blockIndex;
            }).Select(p =>
            {
                JObject state = new JObject();
                state["key"] = p.Key.ToArray().ToHexString();
                state["value"] = p.Value.ToArray().ToHexString();
                return state;
            }));
            File.WriteAllText(path, array.ToString());
            Console.WriteLine($"DumpInBlock States have been dumped into file {path}");
        }

    }

}
