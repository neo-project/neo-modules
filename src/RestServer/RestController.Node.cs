#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Akka.Actor;
using Microsoft.AspNetCore.Mvc;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using System.Linq;

namespace Neo.Plugins
{
    partial class RestController
    {
        /// <summary>
        /// Get the current number of connections for the node
        /// </summary>
        /// <returns></returns>
        [HttpGet("network/localnode/connections")]
        public IActionResult GetConnectionCount()
        {
            return Ok(LocalNode.Singleton.ConnectedCount);
        }

        /// <summary>
        /// Get the peers of the node
        /// </summary>
        /// <returns></returns>
        [HttpGet("network/localnode/peers")]
        public IActionResult GetPeers()
        {
            JObject json = new JObject();
            json["unconnected"] = new JArray(LocalNode.Singleton.GetUnconnectedPeers().Select(p =>
            {
                JObject peerJson = new JObject();
                peerJson["address"] = p.Address.ToString();
                peerJson["port"] = p.Port;
                return peerJson;
            }));
            json["bad"] = new JArray(); //badpeers has been removed
            json["connected"] = new JArray(LocalNode.Singleton.GetRemoteNodes().Select(p =>
            {
                JObject peerJson = new JObject();
                peerJson["address"] = p.Remote.Address.ToString();
                peerJson["port"] = p.ListenerTcpPort;
                return peerJson;
            }));
            return FormatJson(json);
        }

        /// <summary>
        /// Get version of the connected node
        /// </summary>
        /// <returns></returns>
        [HttpGet("network/localnode/version")]
        public IActionResult GetVersion()
        {
            JObject json = new JObject();
            json["tcpPort"] = LocalNode.Singleton.ListenerTcpPort;
            json["wsPort"] = LocalNode.Singleton.ListenerWsPort;
            json["nonce"] = LocalNode.Nonce;
            json["useragent"] = LocalNode.UserAgent;
            return FormatJson(json);
        }

        /// <summary>
        /// Broadcast a transaction over the network
        /// </summary>
        /// <param name="hex">hexstring of the transaction</param>
        /// <returns></returns>
        [HttpPost("transactions/broadcasting")]
        public IActionResult SendRawTransaction(string hex)
        {
            Transaction tx = hex.HexToBytes().AsSerializable<Transaction>();
            RelayResultReason reason = system.Blockchain.Ask<RelayResultReason>(tx).Result;
            return Ok(GetRelayResult(reason, tx.Hash));
        }

        /// <summary>
        /// Relay a new block to the network
        /// </summary>
        /// <param name="hex">hexstring of the block</param>
        /// <returns></returns>
        [HttpPost("validators/submitblock")]
        public IActionResult SubmitBlock(string hex)
        {
            Block block = hex.HexToBytes().AsSerializable<Block>();
            RelayResultReason reason = system.Blockchain.Ask<RelayResultReason>(block).Result;
            return Ok(GetRelayResult(reason, block.Hash));
        }

        private static JObject GetRelayResult(RelayResultReason reason, UInt256 hash)
        {
            if (reason == RelayResultReason.Succeed)
            {
                var ret = new JObject();
                ret["hash"] = hash.ToString();
                return ret;
            }
            else
            {
                throw new RestException(-500, reason.ToString());
            }
        }
    }
}
