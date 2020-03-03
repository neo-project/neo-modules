using Akka.Actor;
using Neo.Consensus;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public class PerformanceMonitor : Plugin, IPersistencePlugin, IP2PPlugin
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
                    return OnNetworkConnectedNodesCommand();
                case "ping":
                case "pingpeers":
                    return OnNetworkPingPeersCommand(args);
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
            Console.WriteLine("\tcheck disk");
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
                var delayInMilliseconds = GetBlockSynchronizationDelay(true);
                if (delayInMilliseconds <= 0)
                {
                    Console.WriteLine("The time to confirm a new block has timed out.");
                }
                else if (delayInMilliseconds < 1000)
                {
                    Console.WriteLine($"Time to synchronize to the last remote block: {delayInMilliseconds:0.#} ms");
                }
                else
                {
                    var delayInSeconds = delayInMilliseconds / 1000.0;
                    Console.WriteLine($"Time to synchronize to the last remote block: {delayInSeconds:0.#} sec");
                }
            }

            return true;
        }

        /// <summary>
        /// Prints the time passed in seconds since the last block
        /// </summary>
        private bool OnBlockTimeSinceLastCommand()
        {
            var timeSinceLastBlockInSec = GetTimeSinceLastBlock() / 1000;
            Console.WriteLine($"Time since last block: {timeSinceLastBlockInSec} seconds");

            return true;
        }

        /// <summary>
        /// Calculates the time passed since the last block
        /// </summary>
        /// <returns>
        /// Returns the time since the last block was persisted in milliseconds
        /// </returns>
        private ulong GetTimeSinceLastBlock()
        {
            var index = Blockchain.Singleton.Height;
            var block = Blockchain.Singleton.GetBlock(index);

            var currentTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return currentTimestamp - block.Timestamp;
        }

        /// <summary>
        /// Calculates the delay in the synchronization of the blocks between the connected nodes
        /// </summary>
        /// <param name="printMessages">
        /// Specifies if the messages should be printed in the console.
        /// </param>
        /// <returns>
        /// If the number of remote nodes is greater than zero, returns the delay in the
        /// synchronization between the local and the remote nodes in milliseconds; otherwise,
        /// returns zero.
        /// </returns>
        private double GetBlockSynchronizationDelay(bool printMessages = false)
        {
            var cancel = new CancellationTokenSource();
            var timeLimitInMilliseconds = 60000; // limit the waiting time to 1 second

            var lastBlockRemote = GetMaxRemoteBlockCount();
            if (lastBlockRemote == 0)
            {
                return 0;
            }

            var lastBlock = Math.Max(lastBlockRemote, Blockchain.Singleton.Height);

            bool showBlock = printMessages;
            DateTime remote = DateTime.Now;
            DateTime local = remote;

            Task monitorRemote = new Task(() =>
            {
                var lastRemoteBlockIndex = WaitPersistedBlock(lastBlock, cancel.Token);
                remote = DateTime.Now;
                if (showBlock && lastRemoteBlockIndex > lastBlock)
                {
                    showBlock = false;
                    Console.WriteLine($"Updated block index to {lastRemoteBlockIndex}");
                }
            }, cancel.Token);

            Task monitorLocal = new Task(() =>
            {
                var lastPersistedBlockIndex = WaitRemoteBlock(lastBlock, cancel.Token);
                local = DateTime.Now;
                if (showBlock && lastPersistedBlockIndex > lastBlock)
                {
                    showBlock = false;
                    Console.WriteLine($"Updated block index to {lastPersistedBlockIndex}");
                }
            }, cancel.Token);

            Task timer = new Task(async () =>
            {
                try
                {
                    await Task.Delay(timeLimitInMilliseconds, cancel.Token);
                    cancel.Cancel();
                }
                catch (OperationCanceledException) { }
            }, cancel.Token);

            if (printMessages)
            {
                Console.WriteLine($"Current block index is {lastBlock}");
                Console.WriteLine("Waiting for the next block...");
            }

            List<Task> tasks = new List<Task>()
            {
                monitorRemote, monitorLocal
            };

            monitorRemote.Start();
            monitorLocal.Start();
            timer.Start();

            try
            {
                Task.WaitAll(tasks.ToArray(), cancel.Token);
                cancel.Cancel();
            }
            catch (OperationCanceledException)
            {
                return 0;
            }

            var delay = remote - local;

            return Math.Abs(delay.TotalMilliseconds);
        }

        /// <summary>
        /// Pauses the current thread until a new block is persisted in the blockchain
        /// </summary>
        /// <param name="blockIndex">
        /// Specifies if the block index to start monitoring.
        /// </param>
        /// <param name="token">
        /// A cancellation token to stop the task if the caller is canceled
        /// </param>
        /// <returns>
        /// If the <paramref name="token"/> is canceled, returns <param name="blockIndex">;
        /// otherwise, returns the index of the persisted block.
        /// </returns>
        private uint WaitPersistedBlock(uint blockIndex, CancellationToken token)
        {
            var persistedBlockIndex = blockIndex;
            var updatePersistedBlock = new TaskCompletionSource<bool>();

            CommitHandler commit = (snapshot) =>
            {
                if (snapshot.Height > blockIndex)
                {
                    persistedBlockIndex = snapshot.Height;
                    updatePersistedBlock.TrySetResult(true);
                }
            };

            OnCommitEvent += commit;
            try
            {
                updatePersistedBlock.Task.Wait(token);
            }
            catch (OperationCanceledException) { }
            OnCommitEvent -= commit;

            return persistedBlockIndex;
        }

        /// <summary>
        /// Pauses the current thread until a new block is received from remote nodes
        /// </summary>
        /// <param name="blockIndex">
        /// Specifies if the block index to start monitoring.
        /// </param>
        /// <param name="token">
        /// A cancellation token to stop the task if the caller is canceled
        /// </param>
        /// <returns>
        /// If the <paramref name="token"/> is canceled, returns <param name="blockIndex">;
        /// otherwise, returns the index of the received block.
        /// </returns>
        private uint WaitRemoteBlock(uint blockIndex, CancellationToken token)
        {
            var remoteBlockIndex = blockIndex;
            var updateRemoteBlock = new TaskCompletionSource<bool>();

            var stopBroadcast = new CancellationTokenSource();

            Task broadcast = Task.Run(() =>
            {
                while (!stopBroadcast.Token.IsCancellationRequested)
                {
                    // receive a PingPayload is what updates RemoteNode LastBlockIndex
                    System.LocalNode.Tell(Message.Create(MessageCommand.Ping, PingPayload.Create(blockIndex)));
                }
            });

            P2PMessageHandler p2pMessage = (message) =>
            {
                if (message.Command == MessageCommand.Pong && message.Payload is PingPayload)
                {
                    var lastBlockIndex = GetMaxRemoteBlockCount();
                    if (lastBlockIndex > remoteBlockIndex && !token.IsCancellationRequested)
                    {
                        remoteBlockIndex = lastBlockIndex;
                        updateRemoteBlock.TrySetResult(true);
                    }
                }
            };

            OnP2PMessageEvent += p2pMessage;
            try
            {
                updateRemoteBlock.Task.Wait(token);
            }
            catch (OperationCanceledException) { }
            stopBroadcast.Cancel();

            OnP2PMessageEvent -= p2pMessage;

            return remoteBlockIndex;
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
                    return OnTransactionSizeCommand(args);
                case "avgsize":
                case "averagesize":
                    return OnTransactionAverageSizeCommand(args);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Process "transaction size" command
        /// Prints the size of the transaction in bytes identified by its hash
        /// </summary>
        private bool OnTransactionSizeCommand(string[] args)
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
        private bool OnTransactionAverageSizeCommand(string[] args)
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

            var cancel = new CancellationTokenSource();
            Task task = Task.Run(async () =>
            {
                while (!cancel.Token.IsCancellationRequested)
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
                    await Task.Delay(1000, cancel.Token);
                }
            });
            Console.ReadLine();
            cancel.Cancel();

            return true;
        }

        /// <summary>
        /// Process "check" command
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
        /// Process "commit time" command
        /// Prints the time in milliseconds to commit in the network
        /// </summary>
        private bool OnCommitTimeCommand()
        {
            var time = GetTimeToCommit(true);
            if (time > 0)
            {
                Console.WriteLine($"Time to commit: {time:0.##} milliseconds");
            }
            else
            {
                Console.WriteLine("Timeout");
            }

            return true;
        }

        /// <summary>
        /// Calculates the time difference between the persist and the commit
        /// </summary>
        /// <param name="printMessages">
        /// Specifies if the messages should be printed in the console.
        /// </param>
        /// <returns>
        /// Returns the difference between the persist time and the commit time
        /// </returns>
        private double GetTimeToCommit(bool printMessages = false)
        {
            var commit = new TaskCompletionSource<bool>();
            var persist = new TaskCompletionSource<bool>();

            DateTime persistTime = DateTime.Now;
            DateTime commitTime = persistTime;

            CommitHandler commitAction = (store) =>
            {
                commitTime = DateTime.Now;
                commit?.TrySetResult(true);
            };
            PersistHandler persistAction = (store, app) =>
            {
                persistTime = DateTime.Now;
                persist?.TrySetResult(true);
            };

            OnCommitEvent += commitAction;
            OnPersistEvent += persistAction;

            if (printMessages)
            {
                Console.WriteLine("Waiting for the next commit...");
            }

            List<Task> tasks = new List<Task>
            {
                commit.Task,
                persist.Task
            };

            var millisecondsToTimeOut = (int)Blockchain.MillisecondsPerBlock;
            Task.WaitAll(tasks.ToArray(), millisecondsToTimeOut);

            OnCommitEvent -= commitAction;
            OnPersistEvent -= persistAction;

            if (!tasks.TrueForAll(task => !task.IsCanceled))
            {
                return 0;
            }

            var timeToCommit = persistTime - commitTime;

            return Math.Abs(timeToCommit.TotalMilliseconds);
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
        /// Process "confirmation time" command
        /// Prints the time in milliseconds to confirm the block
        /// </summary>
        private bool OnConfirmationTimeCommand()
        {
            var time = GetTimeToConfirm(true);

            if (time > 0)
            {
                Console.WriteLine($"Confirmation Time: {time:0.##} milliseconds");
            }
            else
            {
                Console.WriteLine("Timeout");
            }

            return true;
        }

        /// <summary>
        /// Calculates the time difference between the commit request and the actual commit
        /// </summary>
        /// <param name="printMessages">
        /// Specifies if the messages should be printed in the console.
        /// </param>
        /// <returns>
        /// Returns the difference between the commit request time and the actual commit time
        /// </returns>
        private double GetTimeToConfirm(bool printMessages = false)
        {
            var commit = new TaskCompletionSource<bool>(false);
            var consensusMessage = new TaskCompletionSource<bool>(false);

            DateTime consensusMessageTime = DateTime.Now;
            DateTime commitTime = DateTime.Now;

            CommitHandler commitAction = (store) =>
            {
                commitTime = DateTime.Now;
                commit?.TrySetResult(true);
            };
            ConsensusMessageHandler consensusMessageAction = (payload) =>
            {
                if (payload.ConsensusMessage is Commit)
                {
                    consensusMessageTime = DateTime.Now;
                    consensusMessage?.TrySetResult(true);
                }
            };

            OnCommitEvent += commitAction;
            OnConsensusMessageEvent += consensusMessageAction;

            if (printMessages)
            {
                Console.WriteLine("Waiting for the next commit...");
            }

            List<Task<bool>> tasks = new List<Task<bool>>
            {
                commit.Task,
                consensusMessage.Task
            };

            var millisecondsToTimeOut = (int)Blockchain.MillisecondsPerBlock;
            Task.WaitAll(tasks.ToArray(), millisecondsToTimeOut);

            OnCommitEvent -= commitAction;
            OnConsensusMessageEvent -= consensusMessageAction;

            foreach (Task<bool> task in tasks)
            {
                if (!task.IsCompleted)
                {
                    return 0;
                }
            }

            var timeToConfirm = commitTime - consensusMessageTime;

            return Math.Abs(timeToConfirm.TotalMilliseconds);
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
        /// Process "payload time" command
        /// Prints the time in milliseconds to receive a payload
        /// </summary>
        private bool OnPayloadTimeCommand()
        {
            var time = GetPayloadTime(true);
            if (time > 0)
            {
                Console.WriteLine($"Payload Time: {time:0.##} milliseconds");
            }
            else
            {
                Console.WriteLine("Timeout");
            }

            return true;
        }

        /// <summary>
        /// Calculates the time difference between the payload send and receive time
        /// </summary>
        /// <param name="printMessages">
        /// Specifies if the messages should be printed in the console.
        /// </param>
        /// <returns>
        /// Returns the difference between the payload send time and receive time
        /// </returns>
        private double GetPayloadTime(bool printMessages = false)
        {
            var consensusMessage = new TaskCompletionSource<bool>();

            DateTime consensusMessageTime = DateTime.Now;
            DateTime consensusPayloadTime = consensusMessageTime;

            ConsensusMessageHandler consensusMessageAction = (payload) =>
            {
                var timestamp = GetPayloadTimestamp(payload);
                if (timestamp > ulong.MinValue)
                {
                    consensusMessageTime = DateTime.UtcNow;
                    consensusPayloadTime = TimestampToDateTime(timestamp);
                    consensusMessage?.TrySetResult(true);
                }
            };

            OnConsensusMessageEvent += consensusMessageAction;

            if (printMessages)
            {
                Console.WriteLine("Waiting for the next payload...");
            }
            var millisecondsToTimeOut = (int)Blockchain.MillisecondsPerBlock;
            consensusMessage.Task.Wait(millisecondsToTimeOut);

            OnConsensusMessageEvent -= consensusMessageAction;

            if (!consensusMessage.Task.IsCompleted)
            {
                return 0;
            }

            var timeToConfirm = consensusMessageTime - consensusPayloadTime;
            return Math.Abs(timeToConfirm.TotalMilliseconds);
        }

        /// <summary>
        /// Gets the timestamp from a consensus payload
        /// </summary>
        /// <param name="payload">
        /// The payload to get the timestamp
        /// </param>
        /// <returns>
        /// Returns the timestamp of the <paramref name="payload"/> if it has the Timestamp property;
        /// otherwise, returns 0.
        /// </returns>
        private ulong GetPayloadTimestamp(ConsensusPayload payload)
        {
            switch (payload.ConsensusMessage)
            {
                case ChangeView view:
                    return view.Timestamp;
                case PrepareRequest request:
                    return request.Timestamp;
                case RecoveryRequest request:
                    return request.Timestamp;
                default:
                    return ulong.MinValue;
            }
        }

        /// <summary>
        /// Converts a timestamp value to a <see cref="DateTime"/> object
        /// </summary>
        /// <param name="timestamp">
        /// THe timestamp value in milliseconds
        /// </param>
        /// <returns>
        /// If the result is a valid <see cref="DateTime"/>, returns the DateTime object that represents
        /// the timestamp value; otherwise, returns the <see cref="DateTime.UnixEpoch"/>.
        /// </returns>
        private DateTime TimestampToDateTime(ulong timestamp)
        {
            try
            {
                return DateTime.UnixEpoch.AddMilliseconds(timestamp);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTime.UnixEpoch;
            }
        }

        /// <summary>
        /// Prints the number of nodes connected to the local node
        /// </summary>
        private bool OnNetworkConnectedNodesCommand()
        {
            Console.WriteLine($"Connected nodes: {LocalNode.Singleton.ConnectedCount}");
            foreach (RemoteNode node in LocalNode.Singleton.GetRemoteNodes())
            {
                Console.WriteLine($"  ip: {node.Remote.Address,-15}\theight: {node.LastBlockIndex,-8}");
            }
            return true;
        }

        /// <summary>
        /// Send a ping message to the node specified by its IP address.
        /// If none is specified, send a ping message to each peer connected to the local node
        /// </summary>
        private bool OnNetworkPingPeersCommand(string[] args)
        {
            if (args.Length > 2)
            {
                return false;
            }
            else
            {
                if (args.Length == 2)
                {
                    if (args[1] == null || !IPAddress.TryParse(args[1], out var ipaddress))
                    {
                        Console.WriteLine("Invalid parameter");
                        return true;
                    }

                    PingRemoteNode(ipaddress, true);
                }
                else
                {
                    PingAll(true);
                }

                return true;
            }
        }

        /// <summary>
        /// Sends a ping message to the IP addresses of all connected and unconnected peers
        /// </summary>
        /// <param name="printMessages">
        /// Specifies if the messages should be printed in the console.
        /// </param>
        private void PingAll(bool printMessages = false)
        {
            var tasks = new List<Task>();
            if (printMessages)
            {
                Console.WriteLine($"Connected nodes: {LocalNode.Singleton.ConnectedCount}\tUnconnected nodes: {LocalNode.Singleton.UnconnectedCount}");
            }

            // ping remote nodes
            foreach (RemoteNode node in LocalNode.Singleton.GetRemoteNodes())
            {
                Task ping = new Task(() =>
                {
                    var reply = TryPing(node.Remote.Address);
                    if (printMessages && reply != null)
                    {
                        if (reply.Status == IPStatus.Success)
                        {
                            Console.WriteLine($"  {node.Remote.Address,-15}\t{reply.RoundtripTime,-30:###0 ms}\theight: {node.LastBlockIndex,-7}");
                        }
                        else
                        {
                            Console.WriteLine($"  {node.Remote.Address,-15}\t{reply.Status,-30}\theight: {node.LastBlockIndex,-7}");
                        }
                    }
                });
                tasks.Add(ping);
                ping.Start();
            }

            // ping unconnected peers
            foreach (var node in LocalNode.Singleton.GetUnconnectedPeers())
            {
                Task ping = new Task(() =>
                {
                    var reply = TryPing(node.Address);
                    if (printMessages && reply != null)
                    {
                        if (reply.Status == IPStatus.Success)
                        {
                            Console.WriteLine($"  {node.Address,-15}\t{reply.RoundtripTime,-30:###0 ms}\tunconnected");
                        }
                        else
                        {
                            Console.WriteLine($"  {node.Address,-15}\t{reply.Status,-30}\tunconnected");
                        }
                    }
                });
                tasks.Add(ping);
                ping.Start();
            }

            Task.WaitAll(tasks.ToArray());
        }

        /// <summary>
        /// Sends a ping message to specified IP address if it is the address of a remote node
        /// </summary>
        /// <param name="printMessages">
        /// Specifies if the messages should be printed in the console.
        /// </param>
        private void PingRemoteNode(IPAddress ipaddress, bool printMessages = false)
        {
            var remoteNode = GetRemoteNode(ipaddress);
            if (remoteNode == null)
            {
                if (printMessages)
                {
                    Console.WriteLine("Input address was not a connected peer");
                }
                return;
            }

            var reply = TryPing(ipaddress);
            if (printMessages && reply != null)
            {
                if (reply.Status == IPStatus.Success)
                {
                    Console.WriteLine($"  {ipaddress,-15}\t{reply.RoundtripTime,-30:###0 ms}\theight: {remoteNode.LastBlockIndex,-7}");
                }
                else
                {
                    Console.WriteLine($"  {ipaddress,-15}\t{reply.Status,-30}\theight: {remoteNode.LastBlockIndex,-7}");
                }
            }
        }

        /// <summary>
        /// Verifies if a given IP address is the address of a remote node.
        /// </summary>
        /// <param name="ipaddress">
        /// The IP address to be verified.
        /// </param>
        /// <returns>
        /// If the <paramref name="ipaddress"/> is the address of a remote node, returns the
        /// corresponding remote node; otherwise, returns null.
        /// </returns>
        private RemoteNode GetRemoteNode(IPAddress ipaddress)
        {
            foreach (RemoteNode node in LocalNode.Singleton.GetRemoteNodes())
            {
                if (node.Remote.Address.Equals(ipaddress))
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to send a ping message to the specified IP address.
        /// </summary>
        /// <param name="ipaddress">
        /// The IP address to send the ping message.
        /// </param>
        /// <returns>
        /// Returns null if an exception is thrown; otherwise returns the <see cref="PingReply"/>
        /// object that is the response of ping
        /// </returns>
        private PingReply TryPing(IPAddress ipaddress)
        {
            try
            {
                int timeoutInMs = 10000; // ping timeout is 10 seconds

                Ping ping = new Ping();
                var reply = ping.Send(ipaddress, timeoutInMs);

                return reply;
            }
            catch
            {
                return null;
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
        /// Process "rpc time" command
        /// Prints the time in milliseconds to receive the response of a rpc request
        /// </summary>
        private bool OnRpcTimeCommand(string[] args)
        {
            if (args.Length != 3)
            {
                return false;
            }
            else
            {
                var url = args[2];

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    Console.WriteLine("Input url is invalid");
                    return true;
                }

                var responseTime = GetRpcResponseTime(url);

                if (responseTime > 0)
                {
                    Console.WriteLine($"RPC response time: {responseTime:0.##} milliseconds");
                }

                return true;
            }
        }

        /// <summary>
        /// Gets the time to receive the response of a rpc request
        /// </summary>
        /// <param name="url">
        /// The url of the rpc server
        /// </param>
        /// <returns>
        /// Returns zero if any exception is thrown; otherwise, returns the time in milliseconds of
        /// the response from the rpc request
        /// </returns>
        private long GetRpcResponseTime(string url)
        {
            Console.WriteLine($"Sending a RPC request to '{url}'...");
            bool hasThrownException = false;

            RpcClient client = new RpcClient(url);
            Stopwatch watch = Stopwatch.StartNew();

            try
            {
                client.GetBlockCount();
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "An exception was thrown while trying to send the RPC request:\n" +
                    $"\t{e.GetType()}\n" +
                    $"\t{e.Message}");
                hasThrownException = true;
            }

            watch.Stop();

            if (hasThrownException)
            {
                return 0;
            }

            return watch.ElapsedMilliseconds;
        }
    }
}
