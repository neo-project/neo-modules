using Akka.Actor;
using Neo.ConsoleService;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public partial class PerformanceMonitor : Plugin, IPersistencePlugin, IP2PPlugin
    {
        public override string Name => "PerformanceMonitor";

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
                        var total = monitor.CheckAllThreads(true);
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
        /// Process "check threads" command
        /// Prints the number of active threads in the current process
        /// </summary>
        [ConsoleCommand("check threads", Category = "Check Commands", Description = "Show the number of active threads in the current process.")]
        private void OnCheckActiveThreadsCommand()
        {
            var current = Process.GetCurrentProcess();

            Console.WriteLine($"Active threads: {current.Threads.Count}");
        }

        /// <summary>
        /// Process "check memory" command
        /// Prints the amount of memory allocated for the current process in megabytes
        /// </summary>
        [ConsoleCommand("check memory", Category = "Check Commands", Description = "Show the amount of memory allocated for the current process in megabytes.")]
        private void OnCheckMemoryCommand()
        {
            var current = Process.GetCurrentProcess();
            current.Refresh();
            string memoryUnit = "KB";
            var memory = current.WorkingSet64 / 1024.0;

            if (memory > 1024)
            {
                memory = memory / 1024;
                memoryUnit = "MB";
            }

            Console.WriteLine($"Allocated memory: {memory:0.00} {memoryUnit}");
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
