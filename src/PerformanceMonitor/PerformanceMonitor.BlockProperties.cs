using Neo.ConsoleService;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using System;

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
            Block block = null;

            if (UInt256.TryParse(blockIndexOrHash, out var blockHash))
            {
                block = Blockchain.Singleton.GetBlock(blockHash);
            }
            else if (uint.TryParse(blockIndexOrHash, out var blockIndex))
            {
                block = Blockchain.Singleton.GetBlock(blockIndex);
            }

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
    }
}
