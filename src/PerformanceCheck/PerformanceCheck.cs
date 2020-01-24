using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using System;

namespace Neo.Plugins
{
    public class PerformanceCheck : Plugin
    {
        public override string Name => "PerformanceCheck";

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args.Length == 0) return false;
            switch (args[0].ToLower())
            {
                case "help":
                    return OnHelpCommand(args);
                case "block":
                    return OnBlockCommand(args);
            }
            return false;
        }

        /// <summary>
        /// Process "help" command
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnHelpCommand(string[] args)
        {
            if (args.Length < 2)
                return false;

            if (!string.Equals(args[1], Name, StringComparison.OrdinalIgnoreCase))
                return false;

            Console.WriteLine($"{Name} Commands:\n");
            Console.WriteLine("Block Commands:");
            Console.WriteLine("\tblock time <index/hash>");
            Console.WriteLine("\tblock avgtime [2 - 10000]");

            return true;
        }

        /// <summary>
        /// Process "block" command
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnBlockCommand(string[] args)
        {
            if (args.Length < 2) return false;
            switch (args[1].ToLower())
            {
                case "avgtime":
                case "averagetime":
                    return OnBlockAverageTimeCommand(args);
                case "time":
                    return OnBlockTimeCommand(args);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Calculates and prints the average time the latest blocks are active
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnBlockAverageTimeCommand(string[] args)
        {
            if (args.Length > 3)
            {
                return false;
            }
            else
            {
                uint desiredCount;
                if (args.Length == 3)
                {
                    desiredCount = uint.Parse(args[2]);
                    if (desiredCount < 2)
                    {
                        Console.WriteLine("Minimum 2 block");
                        return true;
                    }
                    if (desiredCount > 10000)
                    {
                        Console.WriteLine("Maximum 10000 blocks");
                        return true;
                    }
                }
                else
                {
                    desiredCount = 1000;
                }

                var firstIndex = Blockchain.GenesisBlock.Index;
                using (var snapshot = Blockchain.Singleton.GetSnapshot())
                {
                    var blockHash = snapshot.CurrentBlockHash;
                    var countedBlocks = 0;
                    ulong totaltime = 0;
                    Block block = snapshot.GetBlock(blockHash);

                    ulong nextTimestamp = block.Timestamp;
                    block = snapshot.GetBlock(block.PrevHash);

                    while (block != null && block.Index != firstIndex && desiredCount > countedBlocks)
                    {
                        totaltime += nextTimestamp - block.Timestamp;
                        nextTimestamp = block.Timestamp;

                        block = snapshot.GetBlock(block.PrevHash);
                        countedBlocks++;
                    }

                    double averageInSeconds;
                    if (countedBlocks > 0)
                    {
                        var timeInSeconds = totaltime / 1000.0;
                        averageInSeconds = timeInSeconds / countedBlocks;
                    }
                    else
                    {
                        averageInSeconds = 0.0;
                    }

                    Console.WriteLine(averageInSeconds.ToString("Average time/block: 0.00 seconds"));
                }
                return true;
            }
        }

        /// <summary>
        /// Calculates and prints the block time of the given block index or block hash
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnBlockTimeCommand(string[] args)
        {
            if (args.Length != 3)
            {
                return false;
            }
            else
            {
                string blockId = args[2];
                Block block;

                if (blockId.Length == 66)
                {
                    var blockHash = UInt256.Parse(blockId);
                    block = Blockchain.Singleton.GetBlock(blockHash);
                }
                else
                {
                    var blockIndex = uint.Parse(blockId);
                    block = Blockchain.Singleton.GetBlock(blockIndex);
                }

                if (block != null)
                {
                    var previousBlock = Blockchain.Singleton.GetBlock(block.PrevHash);
                    ulong time = 0;
                    if (previousBlock != null)
                    {
                        time = block.Timestamp - previousBlock.Timestamp;
                    }

                    Console.WriteLine($"Block Hash: {block.Hash}");
                    Console.WriteLine($"      Index: {block.Index}");
                    Console.WriteLine($"      Time: {time / 1000}");
                }
                else
                {
                    Console.WriteLine("Block not found");
                }

                return true;
            }
        }
    }
}
