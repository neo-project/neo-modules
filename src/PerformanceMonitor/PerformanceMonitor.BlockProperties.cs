using Neo.ConsoleService;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using System;
using System.Text;

namespace Neo.Plugins
{
    partial class PerformanceMonitor
    {
        /// <summary>
        /// Process "block time" command
        /// Prints the block time in seconds of the given block index or block hash
        /// </summary>
        [ConsoleCommand("block time", Category = "Block Commands", Description = "Show the block time in seconds of the given block index or block hash.")]
        private void OnBlockTimeCommand(string blockIndexOrHash)
        {
            Block block = GetBlock(blockIndexOrHash);
            if (block == null)
            {
                Console.WriteLine("Block not found");
            }
            else
            {
                ulong time = block.GetTime();

                Console.WriteLine($"Block Hash: {block.Hash}");
                Console.WriteLine($"      Index: {block.Index}");
                Console.WriteLine($"      Time: {time / 1000.0: 0.00} seconds");
            }
        }

        /// <summary>
        /// Gets the block time in seconds of the given block index or block hash
        /// </summary>
        /// Returns the block if the index or hash exists
        /// </returns>
        [RpcMethod]
        public JObject GetBlockTime(JArray _params)
        {
            if (_params.Count != 1)
            {
                throw new RpcException(-32602, "Invalid params");
            }
            var blockIndexOrHash = _params[0].AsString();
            Block block = GetBlock(blockIndexOrHash);

            if (block is null)
                throw new RpcException(-100, "Block not found");

            return block.GetTime();
        }

        /// <summary>
        /// Get a block identified by its index or hash
        /// </summary>
        /// <param name="blockIndexOrHash">
        /// Index or hash of the desired block
        /// </param>
        /// <returns>
        /// Returns the block if the index or hash exists; otherwise,
        /// returns null
        /// </returns>
        private Block GetBlock(string blockIndexOrHash)
        {
            Block block = null;

            if (UInt256.TryParse(blockIndexOrHash, out var blockHash))
            {
                block = Blockchain.Singleton.GetBlock(blockHash);
            }
            else if (uint.TryParse(blockIndexOrHash, out var blockIndex))
            {
                block = Blockchain.Singleton.GetBlock(blockIndex);
            }

            return block;
        }

        /// <summary>
        /// Process "block avgtime" command
        /// Prints the average time in seconds the latest blocks are active
        /// </summary>
        [ConsoleCommand("block avgtime", Category = "Block Commands", Description = "Show the average time in seconds the latest blocks are active.")]
        private void OnBlockAverageTimeCommand(uint blockCount = 1000)
        {
            uint desiredCount = blockCount;

            if (desiredCount < 1)
            {
                Console.WriteLine("Minimum 1 block");
                return;
            }

            if (desiredCount > 10000)
            {
                Console.WriteLine("Maximum 10000 blocks");
                return;
            }

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var averageInSeconds = snapshot.GetAverageTimePerBlock(desiredCount) / 1000;
                Console.WriteLine(averageInSeconds.ToString("Average time/block: 0.00 seconds"));
            }
        }

        /// <summary>
        /// Gets the average time the latest blocks are active
        /// </summary>
        /// <returns>
        /// Returns the average time per block in milliseconds
        /// </returns>
        [RpcMethod]
        public JObject GetBlockAvgTime(JArray _params)
        {
            if (_params.Count > 1)
            {
                throw new RpcException(-32602, "Invalid params");
            }
            uint desiredCount = 1000;
            if (_params.Count > 0)
            {
                if (!uint.TryParse(_params[0].AsString(), out desiredCount))
                {
                    throw new RpcException(-32602, "Invalid params");
                }

                if (desiredCount < 1)
                {
                    throw new RpcException(-100, "Minimum 1 block");
                }

                if (desiredCount > 10000)
                {
                    throw new RpcException(-100, "Maximum 10000 blocks");
                }
            }

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                return snapshot.GetAverageTimePerBlock(desiredCount);
            }
        }
    }
}
