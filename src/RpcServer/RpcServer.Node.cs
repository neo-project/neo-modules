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
            return new RpcPeers
            {
                Unconnected = LocalNode.Singleton.GetUnconnectedPeers().Select(p => new RpcPeer
                {
                    Address = p.Address.ToString(),
                    Port = p.Port
                }).ToArray(),
                Bad = Array.Empty<RpcPeer>(),
                Connected = LocalNode.Singleton.GetRemoteNodes().Select(p => new RpcPeer
                {
                    Address = p.Remote.Address.ToString(),
                    Port = p.ListenerTcpPort
                }).ToArray(),
            }.ToJson();
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
            return new RpcVersion
            {
                TcpPort = LocalNode.Singleton.ListenerTcpPort,
                WsPort = LocalNode.Singleton.ListenerWsPort,
                Nonce = LocalNode.Nonce,
                UserAgent = LocalNode.UserAgent
            }.ToJson();
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
