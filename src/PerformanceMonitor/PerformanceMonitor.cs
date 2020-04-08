using Akka.Actor;
using Neo.ConsoleService;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public partial class PerformanceMonitor : Plugin, IPersistencePlugin, IP2PPlugin
    {
        public override string Name => "PerformanceMonitor";

        public PerformanceMonitor()
        {
            RpcServer.RegisterMethods(this);
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        private delegate void CommitHandler(StoreView snapshot);
        private event CommitHandler OnCommitEvent;

        public void OnCommit(StoreView snapshot)
        {
            OnCommitEvent?.Invoke(snapshot);
        }

        private delegate void PersistHandler(StoreView snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList);
        private event PersistHandler OnPersistEvent;

        public void OnPersist(StoreView snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            OnPersistEvent?.Invoke(snapshot, applicationExecutedList);
        }

        private delegate void P2PMessageHandler(Message message);
        private event P2PMessageHandler OnP2PMessageEvent;

        public bool OnP2PMessage(Message message)
        {
            OnP2PMessageEvent?.Invoke(message);
            return true;
        }

        private delegate void ConsensusMessageHandler(ConsensusPayload payload);
        private event ConsensusMessageHandler OnConsensusMessageEvent;

        public bool OnConsensusMessage(ConsensusPayload payload)
        {
            OnConsensusMessageEvent?.Invoke(payload);
            return true;
        }

        /// <summary>
        /// Process "check cpu" command
        /// Prints each thread CPU usage information every second
        /// </summary>
        [ConsoleCommand("check cpu", Category = "Check Commands", Description = "Show CPU usage information of each thread every second.")]
        private void OnCheckCPUCommand()
        {
            var cancel = new CancellationTokenSource();

            Task task = Task.Run(async () =>
            {
                var monitor = new CpuUsageMonitor();

                while (!cancel.Token.IsCancellationRequested)
                {
                    try
                    {
                        var total = monitor.GetCpuTotalProcessorTime(true);
                        if (!cancel.Token.IsCancellationRequested)
                        {
                            Console.WriteLine($"Active threads: {monitor.ThreadCount,3}\tTotal CPU usage: {total,8:0.00 %}");
                        }

                        await Task.Delay(1000, cancel.Token);
                    }
                    catch
                    {
                        // if any unexpected exception is thrown, stop the loop and finish the task
                        cancel.Cancel();
                    }
                }
            });
            Console.ReadLine();
            cancel.Cancel();
        }

        /// <summary>
        /// Gets each thread CPU usage information
        /// </summary>
        /// <returns>
        /// The total CPU usage and the CPU usage of each active thread in the last second
        /// </returns>
        [RpcMethod]
        public JObject GetCpuUsage(JArray _params)
        {
            if (_params.Count != 0)
            {
                throw new RpcException(-32602, "Invalid params");
            }
            var monitor = new CpuUsageMonitor();

            // wait a second to get the cpu usage info
            Task.Delay(1000).Wait();
            var cpuUsage = monitor.CheckAllThreads();

            var result = new JObject();
            result["totalusage"] = cpuUsage.TotalUsage;

            var threads = new JArray();
            foreach (var threadTime in cpuUsage.ThreadsUsage.OrderByDescending(usage => usage.Value))
            {
                var thread = new JObject();
                thread["id"] = threadTime.Key;
                thread["usage"] = threadTime.Value;
                threads.Add(thread);
            }
            result["threads"] = threads;

            return result;
        }

        /// <summary>
        /// Process "check threads" command
        /// Prints the number of active threads in the current process
        /// </summary>
        [ConsoleCommand("check threads", Category = "Check Commands", Description = "Show the number of active threads in the current process.")]
        private void OnCheckActiveThreadsCommand()
        {
            var threadCount = GetActiveThreadsCount();

            Console.WriteLine($"Active threads: {threadCount}");
        }

        /// <summary>
        /// Gets the number of active threads in the current process
        /// </summary>
        /// <returns>
        /// Returns the number of active threads.
        /// </returns>
        [RpcMethod]
        public JObject GetActiveThreadsCount(JArray _params)
        {
            if (_params.Count != 0)
            {
                throw new RpcException(-32602, "Invalid params");
            }
            return GetActiveThreadsCount();
        }

        /// <summary>
        /// Gets the number of active threads in the current process
        /// </summary>
        /// <returns>
        /// Returns the number of active threads.
        /// </returns>
        private int GetActiveThreadsCount()
        {
            var current = Process.GetCurrentProcess();
            if (current == null || current.Threads == null)
            {
                return 0;
            }

            return current.Threads.Count;
        }

        /// <summary>
        /// Process "check memory" command
        /// Prints the amount of memory allocated for the current process in megabytes
        /// </summary>
        [ConsoleCommand("check memory", Category = "Check Commands", Description = "Show the amount of memory allocated for the current process in megabytes.")]
        private void OnCheckMemoryCommand()
        {
            string memoryUnit = "KB";
            var memory = GetAllocatedMemory() / 1024.0;

            if (memory > 1024)
            {
                memory = memory / 1024;
                memoryUnit = "MB";
            }

            Console.WriteLine($"Allocated memory: {memory:0.00} {memoryUnit}");
        }


        /// <summary>
        /// Gets the amount of memory allocated for the current process in bytes
        /// </summary>
        /// Returns the allocated memory in bytes
        /// </returns>
        [RpcMethod]
        public JObject GetMemory(JArray _params)
        {
            if (_params.Count != 0)
            {
                throw new RpcException(-32602, "Invalid params");
            }
            return GetAllocatedMemory();
        }

        /// <summary>
        /// Gets the amount of memory allocated for the current process in bytes
        /// </summary>
        /// <returns>
        /// Returns the allocated memory in bytes
        /// </returns>
        private long GetAllocatedMemory()
        {
            var current = Process.GetCurrentProcess();
            if (current == null)
            {
                return 0;
            }

            current.Refresh();
            return current.WorkingSet64;
        }

        /// <summary>
        /// Sends a ping message to the blockchain
        /// </summary>
        /// <param name="height">
        /// The block height to be passed in the ping message
        /// </param>
        private void SendBlockchainPingMessage(uint height)
        {
            System.LocalNode.Tell(Message.Create(MessageCommand.Ping, PingPayload.Create(height)));
        }
    }
}
