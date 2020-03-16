using Akka.Actor;
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
                case "commit":
                    return OnCommitCommand(args);
                case "confirm":
                case "confirmation":
                    return OnConfirmationCommand(args);
                case "connected":
                case "connectednodes":
                    return OnConnectedNodesCommand();
                case "ping":
                case "pingpeers":
                    return OnPingPeersCommand(args);
                case "payload":
                    return OnPayloadCommand(args);
                case "rpc":
                    return OnRpcCommand(args);
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
            Console.WriteLine("\tblock timesincelast");
            Console.WriteLine("\tblock sync");
            Console.WriteLine("Check Commands:");
            Console.WriteLine("\tcheck cpu");
            Console.WriteLine("\tcheck memory");
            Console.WriteLine("\tcheck threads");
            Console.WriteLine("Consensus Commands:");
            Console.WriteLine("\tcommit time");
            Console.WriteLine("\tconfirmation time");
            Console.WriteLine("\tpayload time");
            Console.WriteLine("Network Commands:");
            Console.WriteLine("\tconnected");
            Console.WriteLine("\tping [ipaddress]");
            Console.WriteLine("\trpc time <url>");
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
                case "sincelast":
                case "timelast":
                case "timesincelast":
                    return OnBlockTimeSinceLastCommand();
                default:
                    return false;
            }
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
                    return OnTransactionSizeCommand(args);
                case "avgsize":
                case "averagesize":
                    return OnTransactionAverageSizeCommand(args);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Process "rpc" command
        /// </summary>
        private bool OnRpcCommand(string[] args)
        {
            if (args.Length < 2) return false;
            switch (args[1].ToLower())
            {
                case "time":
                    return OnRpcTimeCommand(args);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Process "commit" command
        /// </summary>
        private bool OnCommitCommand(string[] args)
        {
            if (args.Length < 2) return false;
            switch (args[1].ToLower())
            {
                case "time":
                    return OnCommitTimeCommand();
                default:
                    return false;
            }
        }

        /// <summary>
        /// Process "confirmation" command
        /// </summary>
        private bool OnConfirmationCommand(string[] args)
        {
            if (args.Length < 2) return false;
            switch (args[1].ToLower())
            {
                case "time":
                    return OnConfirmationTimeCommand();
                default:
                    return false;
            }
        }

        /// <summary>
        /// Process "payload" command
        /// </summary>
        private bool OnPayloadCommand(string[] args)
        {
            if (args.Length < 2) return false;
            switch (args[1].ToLower())
            {
                case "time":
                    return OnPayloadTimeCommand();
                default:
                    return false;
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
        private bool OnCheckMemoryCommand()
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

            return true;
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
