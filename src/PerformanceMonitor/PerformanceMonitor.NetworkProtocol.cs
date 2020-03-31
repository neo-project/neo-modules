using Neo.ConsoleService;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.RPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    partial class PerformanceMonitor
    {
        /// <summary>
        /// Prints the time passed in seconds since the last block
        /// </summary>
        [ConsoleCommand("block timesincelast", Category = "Block Commands", Description = "Show the time passed in seconds since the last block.")]
        private void OnBlockTimeSinceLastCommand()
        {
            var timeSinceLastBlockInSec = GetTimeSinceLastBlock() / 1000;
            Console.WriteLine($"Time since last block: {timeSinceLastBlockInSec} seconds");
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
        [ConsoleCommand("connected", Category = "Network Commands", Description = "Show the number of nodes connected to the local node.")]
        private void OnConnectedNodesCommand()
        {
            Console.WriteLine($"Connected nodes: {LocalNode.Singleton.ConnectedCount}");

            UpdateRemotesHeight();

            foreach (RemoteNode node in LocalNode.Singleton.GetRemoteNodes())
            {
                var remoteAddressAndPort = $"{node.Remote.Address}:{node.Remote.Port}";
                Console.WriteLine($"  ip: {remoteAddressAndPort,-25}\theight: {node.LastBlockIndex,-8}");
            }
        }

        /// <summary>
        /// Updates the remote nodes block height
        /// </summary>
        private void UpdateRemotesHeight()
        {
            var remotesHeightUpdate = new TaskCompletionSource<bool>();

            P2PMessageHandler p2pMessage = (message) =>
            {
                remotesHeightUpdate.TrySetResult(true);
            };

            OnP2PMessageEvent += p2pMessage;
            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                // updates the height of the remote nodes
                SendBlockchainPingMessage(snapshot.Height);
            }

            // waits the ping message response to continue
            remotesHeightUpdate.Task.Wait(1000);
            OnP2PMessageEvent -= p2pMessage;
        }

        /// <summary>
        /// Send a ping message to the node specified by its IP address.
        /// If none is specified, send a ping message to each peer connected to the local node
        /// </summary>
        [ConsoleCommand("ping", Category = "Network Commands",
            Description = "Send a ping message to the node specified by its IP address.\n" +
                          "If none is specified, send a ping message to each peer connected to the local node")]
        private void OnPingPeersCommand(string ipaddress = null)
        {
            UpdateRemotesHeight();
            if (ipaddress != null)
            {
                if (!IPAddress.TryParse(ipaddress, out var address))
                {
                    Console.WriteLine("Invalid parameter");
                    return;
                }

                PingRemoteNode(address, true);
            }
            else
            {
                PingAll(true);
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
                        var remoteAddressAndPort = $"{node.Remote.Address}:{node.Remote.Port}";
                        if (reply.Status == IPStatus.Success)
                        {
                            Console.WriteLine($"  {remoteAddressAndPort,-25}\theight: {node.LastBlockIndex,-8}\t{reply.RoundtripTime,-30:###0 ms}");
                        }
                        else
                        {
                            Console.WriteLine($"  {remoteAddressAndPort,-25}\theight: {node.LastBlockIndex,-8}\t{reply.Status,-30}");
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
                        var remoteAddressAndPort = $"{node.Address}:{node.Port}";
                        if (reply.Status == IPStatus.Success)
                        {
                            Console.WriteLine($"  {remoteAddressAndPort,-25}\t{"unconnected",-16}\t{reply.RoundtripTime,-30:###0 ms}");
                        }
                        else
                        {
                            Console.WriteLine($"  {remoteAddressAndPort,-25}\t{"unconnected",-16}\t{reply.Status,-30}");
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
            var remoteNodes = GetRemoteNode(ipaddress);
            if (remoteNodes == null || remoteNodes.Count == 0)
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
                foreach (var node in remoteNodes)
                {
                    var remoteAddressAndPort = $"{ipaddress}:{node.Remote.Port}";
                    if (reply.Status == IPStatus.Success)
                    {
                        Console.WriteLine($"  {remoteAddressAndPort,-25}\theight: {node.LastBlockIndex,-8}\t{reply.RoundtripTime,-30:###0 ms}");
                    }
                    else
                    {
                        Console.WriteLine($"  {remoteAddressAndPort,-25}\theight: {node.LastBlockIndex,-8}\t{reply.Status,-30}");
                    }
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
        /// If the <paramref name="ipaddress"/> is the address of a remote node, returns a list
        /// of the remote nodes with that address; otherwise, returns an empty list.
        /// </returns>
        private List<RemoteNode> GetRemoteNode(IPAddress ipaddress)
        {
            List<RemoteNode> nodes = new List<RemoteNode>();
            foreach (RemoteNode node in LocalNode.Singleton.GetRemoteNodes())
            {
                if (node.Remote.Address.Equals(ipaddress))
                {
                    nodes.Add(node);
                }
            }

            return nodes;
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
        /// <param name="url">
        /// The url of the rpc server
        /// </param>
        [ConsoleCommand("rpc time", Category = "Network Commands", Description = "Show the time in milliseconds to receive the response of a rpc request.")]
        private void OnRpcTimeCommand(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Console.WriteLine("Input url is invalid");
                return;
            }

            try
            {
                var responseTime = GetRpcResponseTime(url);

                if (responseTime > 0)
                {
                    Console.WriteLine($"RPC response time: {responseTime:0.##} milliseconds");
                }
            }
            catch (FileNotFoundException)
            {
                // for this command it is required that the RpcClient plugin is installed
                Console.WriteLine("Install RpcClient module to use the rpc time command.");
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
            catch (HttpRequestException)
            {
                Console.WriteLine("Input url is not a the url of a valid RPC server");
                hasThrownException = true;
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
