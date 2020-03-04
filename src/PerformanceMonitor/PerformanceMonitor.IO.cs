using Akka.Actor;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    partial class PerformanceMonitor
    {
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
            var timeLimitInMilliseconds = 60 * 1000; // limit the waiting time to 1 minute

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
    }
}
