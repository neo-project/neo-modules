using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

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
                case "tx":
                case "transaction":
                    return OnTransactionCommand(args);
                case "check":
                    return OnCheckCommand(args);
            }
            return false;
        }

        /// <summary>
        /// Process "help" command
        /// </summary>
        private bool OnHelpCommand(string[] args)
        {
            if (args.Length < 2)
                return false;

            if (!string.Equals(args[1], Name, StringComparison.OrdinalIgnoreCase))
                return false;

            Console.WriteLine($"{Name} Commands:\n");
            Console.WriteLine("Block Commands:");
            Console.WriteLine("\tblock time <index/hash>");
            Console.WriteLine("\tblock avgtime [1 - 10000]");
            Console.WriteLine("\tblock sync");
            Console.WriteLine("Check Commands:");
            Console.WriteLine("\tcheck disk");
            Console.WriteLine("\tcheck cpu");
            Console.WriteLine("\tcheck memory");
            Console.WriteLine("\tcheck threads");
            Console.WriteLine("Transaction Commands:");
            Console.WriteLine("\ttx size <hash>");
            Console.WriteLine("\ttx avgsize [1 - 10000]");

            return true;
        }

        /// <summary>
        /// Process "block" command
        /// </summary>
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
                case "sync":
                case "synchronization":
                    return OnBlockSynchronizationCommand();
                default:
                    return false;
            }
        }

        /// <summary>
        /// Process "block avgtime" command
        /// Prints the average time in seconds the latest blocks are active
        /// </summary>
        private bool OnBlockAverageTimeCommand(string[] args)
        {
            if (args.Length > 3)
            {
                return false;
            }
            else
            {
                uint desiredCount = 1000;
                if (args.Length == 3)
                {
                    if (!uint.TryParse(args[2], out desiredCount))
                    {
                        Console.WriteLine("Invalid parameter");
                        return true;
                    }

                    if (desiredCount < 1)
                    {
                        Console.WriteLine("Minimum 1 block");
                        return true;
                    }

                    if (desiredCount > 10000)
                    {
                        Console.WriteLine("Maximum 10000 blocks");
                        return true;
                    }
                }

                using (var snapshot = Blockchain.Singleton.GetSnapshot())
                {
                    var averageInSeconds = snapshot.GetAverageTimePerBlock(desiredCount) / 1000;
                    Console.WriteLine(averageInSeconds.ToString("Average time/block: 0.00 seconds"));
                }

                return true;
            }
        }

        /// <summary>
        /// Process "block time" command
        /// Prints the block time in seconds of the given block index or block hash
        /// </summary>
        private bool OnBlockTimeCommand(string[] args)
        {
            if (args.Length != 3)
            {
                return false;
            }
            else
            {
                string blockId = args[2];
                Block block = null;

                if (UInt256.TryParse(blockId, out var blockHash))
                {
                    block = Blockchain.Singleton.GetBlock(blockHash);
                }
                else if (uint.TryParse(blockId, out var blockIndex))
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
                    Console.WriteLine($"      Time: {time / 1000} seconds");
                }

                return true;
            }
        }

        /// <summary>
        /// Process "block sync" command
        /// Prints the delay in seconds in the synchronization of the blocks in the network
        /// </summary>
        private bool OnBlockSynchronizationCommand()
        {
            var lastBlockRemote = GetMaxRemoteBlockCount();

            if (lastBlockRemote == 0)
            {
                Console.WriteLine("There are no remote nodes to synchronize the local chain");
            }
            else
            {
                Console.WriteLine("Waiting for the next block...");
                var delayInSeconds = GetBlockSynchronizationDelay() / 1000.0;
                Console.WriteLine($"Time to synchronize to the last remote block: {delayInSeconds:0.#} sec");
            }

            return true;
        }

        /// <summary>
        /// Calculates the delay in the synchronization of the blocks in the network
        /// </summary>
        /// <returns>
        /// If the number of remote nodes is greater than zero, returns the delay in the
        /// synchronization between the local and the remote nodes in milliseconds; otherwise,
        /// returns zero.
        /// </returns>
        private long GetBlockSynchronizationDelay()
        {
            var lastBlockRemote = GetMaxRemoteBlockCount();
            if (lastBlockRemote == 0)
            {
                return 0;
            }

            var lastBlockLocal = Blockchain.Singleton.Height;

            long remoteDelay = 0;
            long localDelay = 0;
            long delay = 0;

            Task monitorRemote = new Task(() =>
            {
                var currentBlockRemote = lastBlockRemote;
                do
                {
                    // just wait for the next remote block
                    currentBlockRemote = GetMaxRemoteBlockCount();
                } while (lastBlockRemote == currentBlockRemote);

                Stopwatch watch = Stopwatch.StartNew();
                while (currentBlockRemote > Blockchain.Singleton.Height)
                {
                    // just wait for the next local block
                }
                watch.Stop();
                remoteDelay = watch.ElapsedMilliseconds;
            });
            Task monitorLocal = new Task(() =>
            {
                var currentBlockLocal = lastBlockLocal;
                do
                {
                    // just wait for the next local block
                    currentBlockLocal = Blockchain.Singleton.Height;
                } while (lastBlockLocal == currentBlockLocal);

                Stopwatch watch = Stopwatch.StartNew();
                while (currentBlockLocal > GetMaxRemoteBlockCount())
                {
                    // just wait for the next next block
                }
                watch.Stop();
                localDelay = watch.ElapsedMilliseconds;
            });

            if (lastBlockRemote <= lastBlockLocal)
            {
                // the local node is fully synchronized
                monitorLocal.Start();
                monitorRemote.Start();

                Task.WaitAll(monitorLocal, monitorRemote);
                delay = Math.Max(remoteDelay, localDelay);
            }
            else
            {
                // the local node is synchronizing
                Stopwatch watch = Stopwatch.StartNew();
                while (lastBlockRemote > Blockchain.Singleton.Height)
                {
                    // just wait for local node synchronize
                }
                watch.Stop();
                delay = watch.ElapsedMilliseconds;
            }

            return delay;
        }

        /// <summary>
        /// Gets the block count of the remote node with the highest height
        /// </summary>
        /// <returns>
        /// If the number of remote nodes is greater than zero, returns block count of the
        /// node with the highest height; otherwise returns zero.
        /// </returns>
        private uint GetMaxRemoteBlockCount()
        {
            var remotes = LocalNode.Singleton.GetRemoteNodes();
            uint maxCount = 0;

            foreach (var node in remotes)
            {
                if (node.LastBlockIndex > maxCount)
                {
                    maxCount = node.LastBlockIndex;
                }
            }

            return maxCount;
        }

        /// <summary>
        /// Process "transaction" command
        /// </summary>
        private bool OnTransactionCommand(string[] args)
        {
            if (args.Length < 2) return false;
            switch (args[1].ToLower())
            {
                case "size":
                    return OnTransactionSize(args);
                case "avgsize":
                case "averagesize":
                    return OnTransactionAverageSize(args);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Process "transaction size" command
        /// Prints the size of the transaction in bytes identified by its hash
        /// </summary>
        private bool OnTransactionSize(string[] args)
        {
            if (args.Length != 3)
            {
                return false;
            }
            else
            {
                using (var snapshot = Blockchain.Singleton.GetSnapshot())
                {
                    Transaction tx = null;
                    if (UInt256.TryParse(args[2], out var transactionHash))
                    {
                        tx = snapshot.GetTransaction(transactionHash);
                    }

                    if (tx == null)
                    {
                        Console.WriteLine("Transaction not found");
                    }
                    else
                    {
                        Console.WriteLine($"Transaction Hash: {tx.Hash}");
                        Console.WriteLine($"            Size: {tx.Size} bytes");
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Process "transaction avgsize" command
        /// Prints the average size in bytes of the latest transactions
        /// </summary>
        private bool OnTransactionAverageSize(string[] args)
        {
            if (args.Length > 3)
            {
                return false;
            }
            else
            {
                uint desiredCount = 1000;
                if (args.Length == 3)
                {
                    if (!uint.TryParse(args[2], out desiredCount))
                    {
                        Console.WriteLine("Invalid parameter");
                        return true;
                    }

                    if (desiredCount < 1)
                    {
                        Console.WriteLine("Minimum 1 transaction");
                        return true;
                    }

                    if (desiredCount > 10000)
                    {
                        Console.WriteLine("Maximum 10000 transactions");
                        return true;
                    }
                }

                var averageInKbytes = GetSizePerTransaction(desiredCount);
                Console.WriteLine(averageInKbytes.ToString("Average size/tx: 0 bytes"));
            }

            return true;
        }

        /// <summary>
        /// Returns the average size of the latest transactions
        /// </summary>
        /// <param name="desiredCount">
        /// The desired number of transactions that should be checked to calculate the average size
        /// </param>
        /// <returns>
        /// Returns the average size per transaction in bytes if the number of analysed transactions
        /// is greater than zero; otherwise, returns 0.0
        /// </returns>
        private double GetSizePerTransaction(uint desiredCount)
        {
            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var blockHash = snapshot.CurrentBlockHash;
                var countedTxs = 0;

                Block block = snapshot.GetBlock(blockHash);
                int totalsize = 0;

                do
                {
                    foreach (var tx in block.Transactions)
                    {
                        if (tx != null)
                        {
                            totalsize += tx.Size;
                            countedTxs++;

                            if (desiredCount <= countedTxs)
                            {
                                break;
                            }
                        }
                    }

                    block = snapshot.GetBlock(block.PrevHash);
                } while (block != null && desiredCount > countedTxs);

                double averageSize = 0.0;
                if (countedTxs > 0)
                {
                    averageSize = 1.0 * totalsize / countedTxs;
                }

                return averageSize;
            }
        }

        /// <summary>
        /// Process "check" command
        /// </summary>
        private bool OnCheckCommand(string[] args)
        {
            if (args.Length < 2) return false;
            switch (args[1].ToLower())
            {
                case "disk":
                    return OnCheckDiskCommand();
                case "cpu":
                    return OnCheckCPUCommand();
                case "threads":
                case "activethreads":
                    return OnCheckActiveThreadsCommand();
                case "mem":
                case "memory":
                    return OnCheckMemoryCommand();
                default:
                    return false;
            }
        }

        /// <summary>
        /// Process "check cpu" command
        /// Prints each thread CPU usage information every second
        /// </summary>
        private bool OnCheckCPUCommand()
        {
            bool run = true;

            Task task = Task.Run(async () =>
            {
                var monitor = new CpuUsageMonitor();

                while (run)
                {
                    try
                    {
                        var total = monitor.CheckAllThreads(run);
                        if (run)
                        {
                            Console.WriteLine($"Active threads: {monitor.ThreadCount,3}\tTotal CPU usage: {total,8:0.00 %}");
                        }

                        await Task.Delay(1000);
                    }
                    catch
                    {
                        // if any unexpected exception is thrown, stop the loop and finish the task
                        run = false;
                    }
                }
            });
            Console.ReadLine();

            run = false;
            task.Wait();

            return true;
        }

        /// <summary>
        /// Process "check threads" command
        /// Prints the number of active threads in the current process
        /// </summary>
        private bool OnCheckActiveThreadsCommand()
        {
            var current = Process.GetCurrentProcess();

            Console.WriteLine($"Active threads: {current.Threads.Count}");

            return true;
        }

        /// <summary>
        /// Process "check memory" command
        /// Prints the amount of memory allocated for the current process in megabytes
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnCheckMemoryCommand()
        {
            var current = Process.GetCurrentProcess();
            var memoryInMB = current.PagedMemorySize64 / 1024 / 1024.0;

            Console.WriteLine($"Allocated memory: {memoryInMB:0.00} MB");

            return true;
        }

        /// <summary>
        /// Process "check disk" command
        /// Prints the disk access information
        /// </summary>
        private bool OnCheckDiskCommand()
        {
            var megabyte = 1024;

            var writePerSec = new PerformanceCounter("Process", "IO Write Bytes/sec", "_Total");
            var readPerSec = new PerformanceCounter("Process", "IO Read Bytes/sec", "_Total");

            bool run = true;
            Task task = Task.Run(async () =>
            {
                while (run)
                {
                    Console.Clear();
                    string diskWriteUnit = "KB/s";
                    string diskReadUnit = "KB/s";

                    var diskWritePerSec = Convert.ToInt32(writePerSec.NextValue()) / 1024.0;
                    var diskReadPerSec = Convert.ToInt32(readPerSec.NextValue()) / 1024.0;

                    if (diskWritePerSec > megabyte)
                    {
                        diskWritePerSec = diskWritePerSec / 1024;
                        diskWriteUnit = "MB/s";
                    }
                    if (diskReadPerSec > megabyte)
                    {
                        diskReadPerSec = diskReadPerSec / 1024;
                        diskReadUnit = "MB/s";
                    }

                    Console.WriteLine($"Disk write: {diskWritePerSec:0.0#} {diskWriteUnit}");
                    Console.WriteLine($"Disk read:  {diskReadPerSec:0.0#} {diskReadUnit}");
                    await Task.Delay(1000);
                }
            });
            Console.ReadLine();
            run = false;

            return true;
        }
    }
}
