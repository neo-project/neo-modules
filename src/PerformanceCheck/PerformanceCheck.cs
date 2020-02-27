using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
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
            Console.WriteLine("Check Commands:");
            Console.WriteLine("\tcheck cpu");
            Console.WriteLine("\tcheck memory");
            Console.WriteLine("\tcheck threads");

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
        /// Process "check" command
        /// </summary>
        private bool OnCheckCommand(string[] args)
        {
            if (args.Length < 2) return false;
            switch (args[1].ToLower())
            {
                case "cpu":
                    return OnCheckCPUCommand(args);
                case "threads":
                case "activethreads":
                    return OnCheckActiveThreadsCommand(args);
                case "mem":
                case "memory":
                    return OnCheckMemoryCommand(args);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Process "check cpu" command
        /// Prints each thread CPU usage information every second
        /// </summary>
        private bool OnCheckCPUCommand(string[] args)
        {
            if (args.Length > 3)
            {
                return false;
            }
            else
            {
                var intervalInMilliseconds = 1000;

                bool run = true;
                Task task = Task.Run(async () =>
                {
                    // key: thread Id       value: thread total processor time in milliseconds
                    var threadsProcessorTime = new Dictionary<int, double>();
                    var watch = Stopwatch.StartNew();

                    while (run)
                    {
                        long totalTime = watch.ElapsedMilliseconds;
                        try
                        {
                            var processName = Process.GetCurrentProcess().ProcessName;
                            string result = "";

                            foreach (ProcessThread thread in Process.GetCurrentProcess().Threads)
                            {
                                try
                                {
                                    var newTime = thread.TotalProcessorTime.TotalMilliseconds;
                                    if (!threadsProcessorTime.TryGetValue(thread.Id, out var oldTime))
                                    {
                                        threadsProcessorTime.Add(thread.Id, newTime);
                                        oldTime = newTime;
                                    }

                                    totalTime += watch.ElapsedMilliseconds;
                                    var cpuUsage = (newTime - oldTime) / totalTime;
                                    result += $"{processName}/{thread.Id,-8} CPU usage: {cpuUsage,8:0.00 %}\n";
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.Message);
                                    if (threadsProcessorTime.ContainsKey(thread.Id))
                                    {
                                        threadsProcessorTime.Remove(thread.Id);
                                    }
                                }
                            }

                            Console.Clear();
                            Console.Write(result);
                            await Task.Delay(intervalInMilliseconds);
                        }
                        catch
                        {
                            run = false;
                        }
                    }
                    watch.Stop();
                });
                Console.ReadLine();

                run = false;
                task.Wait();

                return true;
            }
        }

        /// <summary>
        /// Process "check threads" command
        /// Prints the number of active threads in the current process
        /// </summary>
        private bool OnCheckActiveThreadsCommand(string[] args)
        {
            if (args.Length != 2)
            {
                return false;
            }
            else
            {
                var current = Process.GetCurrentProcess();

                Console.WriteLine($"Active threads: {current.Threads.Count}");

                return true;
            }
        }

        /// <summary>
        /// Process "check memory" command
        /// Prints the amount of memory allocated for the current process in megabytes
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnCheckMemoryCommand(string[] args)
        {
            if (args.Length != 2)
            {
                return false;
            }
            else
            {
                var current = Process.GetCurrentProcess();
                var memoryInMB = current.PagedMemorySize64 / 1024 / 1024.0;

                Console.WriteLine($"Allocated memory: {memoryInMB:0.00} MB");

                return true;
            }
        }
    }
}
