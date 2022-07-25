// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.IO;
using Neo.Json;
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
        protected virtual JToken GetConnectionCount(JArray _params)
        {
            return localNode.ConnectedCount;
        }

        [RpcMethod]
        protected virtual JToken GetPeers(JArray _params)
        {
            var json = new JObject();
            json["unconnected"] = new JArray(localNode.GetUnconnectedPeers().Select(p =>
            {
                var peerJson = new JObject();
                peerJson["address"] = p.Address.ToString();
                peerJson["port"] = p.Port;
                return peerJson;
            }));
            json["bad"] = new JArray(); //badpeers has been removed
            json["connected"] = new JArray(localNode.GetRemoteNodes().Select(p =>
            {
                var peerJson = new JObject();
                peerJson["address"] = p.Remote.Address.ToString();
                peerJson["port"] = p.ListenerTcpPort;
                return peerJson;
            }));
            return json;
        }

        private static JToken GetRelayResult(VerifyResult reason, UInt256 hash)
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
        protected virtual JToken GetVersion(JArray _params)
        {
            var json = new JObject();
            json["tcpport"] = localNode.ListenerTcpPort;
            json["wsport"] = localNode.ListenerWsPort;
            json["nonce"] = LocalNode.Nonce;
            json["useragent"] = LocalNode.UserAgent;
            json["protocol"] = new JObject();
            json["protocol"]["addressversion"] = system.Settings.AddressVersion;
            json["protocol"]["network"] = system.Settings.Network;
            json["protocol"]["validatorscount"] = system.Settings.ValidatorsCount;
            json["protocol"]["msperblock"] = system.Settings.MillisecondsPerBlock;
            json["protocol"]["maxtraceableblocks"] = system.Settings.MaxTraceableBlocks;
            json["protocol"]["maxvaliduntilblockincrement"] = system.Settings.MaxValidUntilBlockIncrement;
            json["protocol"]["maxtransactionsperblock"] = system.Settings.MaxTransactionsPerBlock;
            json["protocol"]["memorypoolmaxtransactions"] = system.Settings.MemoryPoolMaxTransactions;
            json["protocol"]["initialgasdistribution"] = system.Settings.InitialGasDistribution;
            return json;
        }

        [RpcMethod]
        protected virtual JToken SendRawTransaction(JArray _params)
        {
            Transaction tx = Convert.FromBase64String(_params[0].AsString()).AsSerializable<Transaction>();
            RelayResult reason = system.Blockchain.Ask<RelayResult>(tx).Result;
            return GetRelayResult(reason.Result, tx.Hash);
        }

        [RpcMethod]
        protected virtual JToken SubmitBlock(JArray _params)
        {
            Block block = Convert.FromBase64String(_params[0].AsString()).AsSerializable<Block>();
            RelayResult reason = system.Blockchain.Ask<RelayResult>(block).Result;
            return GetRelayResult(reason.Result, block.Hash);
        }
    }
}
