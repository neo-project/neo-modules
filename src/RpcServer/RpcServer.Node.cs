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
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins
{
    partial class RpcServer
    {
        [RpcMethod]
        protected virtual JObject GetConnectionCount(JArray _params)
        {
            return LocalNode.Singleton.ConnectedCount;
        }

        [RpcMethod]
        protected virtual JObject GetPeers(JArray _params)
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
        protected virtual JObject GetVersion(JArray _params)
        {
            JObject json = new JObject();
            json["tcpport"] = LocalNode.Singleton.ListenerTcpPort;
            json["wsport"] = LocalNode.Singleton.ListenerWsPort;
            json["nonce"] = LocalNode.Nonce;
            json["useragent"] = LocalNode.UserAgent;
            json["magic"] = ProtocolSettings.Default.Magic;
            return json;
        }

        [RpcMethod]
        protected virtual JObject SendRawTransaction(JArray _params)
        {
            Transaction tx = Convert.FromBase64String(_params[0].AsString()).AsSerializable<Transaction>();
            RelayResult reason = system.Blockchain.Ask<RelayResult>(tx).Result;
            return GetRelayResult(reason.Result, tx.Hash);
        }

        [RpcMethod]
        protected virtual JObject SubmitBlock(JArray _params)
        {
            Block block = Convert.FromBase64String(_params[0].AsString()).AsSerializable<Block>();
            RelayResult reason = system.Blockchain.Ask<RelayResult>(block).Result;
            return GetRelayResult(reason.Result, block.Hash);
        }
    }
}
