#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Akka.Actor;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins
{
    partial class RpcServer
    {
        [RpcMethod]
        private JObject GetConnectionCount(JArray _params)
        {
            return LocalNode.Singleton.ConnectedCount;
        }

        [RpcMethod]
        private JObject GetPeers(JArray _params)
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
            return json;
        }

        private static JObject GetRelayResult(VerifyResult reason, UInt256 hash)
        {
            if (reason == VerifyResult.Succeed)
            {
                var ret = new JObject();
                ret["hash"] = hash.ToString();
                return ret;
            }
            else
            {
                throw new RpcException(-500, reason.ToString());
            }
        }

        [RpcMethod]
        private JObject GetVersion(JArray _params)
        {
            JObject json = new JObject();
            json["tcp_port"] = LocalNode.Singleton.ListenerTcpPort;
            json["ws_port"] = LocalNode.Singleton.ListenerWsPort;
            json["nonce"] = LocalNode.Nonce;
            json["user_agent"] = LocalNode.UserAgent;
            return json;
        }

        [RpcMethod]
        private JObject SendRawTransaction(JArray _params)
        {
            Transaction tx = _params[0].AsString().HexToBytes().AsSerializable<Transaction>();
            return Send(tx);
        }

        [RpcMethod]
        private JObject SubmitBlock(JArray _params)
        {
            Block block = _params[0].AsString().HexToBytes().AsSerializable<Block>();
            return Send(block);
        }

        private JObject Send(IInventory inventory)
        {
            System.Blockchain.Tell(inventory);

            int timeOut = 1000;
            DateTime current = DateTime.Now;
            while (rpcActor.Ask<RelayResult>(0).Result == null && DateTime.Now.Subtract(current).Milliseconds < timeOut) { Task.Delay(50); }
            var result = rpcActor.Ask<RelayResult>(0).Result;
            return GetRelayResult(result.Result, inventory.Hash);
        }
    }
}
