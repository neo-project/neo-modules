#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Akka.Actor;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using System.Linq;

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
                throw new RpcException(-500, reason.ToString());
            }
        }

        [RpcMethod]
        private JObject GetVersion(JArray _params)
        {
            JObject json = new JObject();
            json["tcpPort"] = LocalNode.Singleton.ListenerTcpPort;
            json["wsPort"] = LocalNode.Singleton.ListenerWsPort;
            json["nonce"] = LocalNode.Nonce;
            json["useragent"] = LocalNode.UserAgent;
            return json;
        }

        [RpcMethod]
        private JObject SendRawTransaction(JArray _params)
        {
            Transaction tx = _params[0].AsString().HexToBytes().AsSerializable<Transaction>();
            RelayResultReason reason = System.Blockchain.Ask<RelayResultReason>(tx).Result;
            return GetRelayResult(reason, tx.Hash);
        }

        [RpcMethod]
        private JObject SubmitBlock(JArray _params)
        {
            Block block = _params[0].AsString().HexToBytes().AsSerializable<Block>();
            RelayResultReason reason = System.Blockchain.Ask<RelayResultReason>(block).Result;
            return GetRelayResult(reason, block.Hash);
        }
    }
}
