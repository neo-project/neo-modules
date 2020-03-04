using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.RPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    partial class PerformanceMonitor
    {
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
        /// Prints the number of nodes connected to the local node
        /// </summary>
        private bool OnConnectedNodesCommand()
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
        private bool OnPingPeersCommand(string[] args)
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
