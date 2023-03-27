// Copyright (C) 2015-2023 The Neo Project.
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
        protected virtual JToken GetConnectionCount(JArray @params)
        {
            return _localNode.ConnectedCount;
        }

        [RpcMethod]
        protected virtual JToken GetPeers(JArray @params)
        {
            JObject json = new();
            json["unconnected"] = new JArray(_localNode.GetUnconnectedPeers().Select(p =>
            {
                JObject peerJson = new();
                peerJson["address"] = p.Address.ToString();
                peerJson["port"] = p.Port;
                return peerJson;
            }));
            json["bad"] = new JArray(); //badpeers has been removed
            json["connected"] = new JArray(_localNode.GetRemoteNodes().Select(p =>
            {
                JObject peerJson = new();
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
        protected virtual JToken GetVersion(JArray @params)
        {
            JObject json = new();
            json["tcpport"] = _localNode.ListenerTcpPort;
            json["wsport"] = _localNode.ListenerWsPort;
            json["nonce"] = LocalNode.Nonce;
            json["useragent"] = LocalNode.UserAgent;
            json["protocol"] = new JObject();
            json["protocol"]["addressversion"] = _system.Settings.AddressVersion;
            json["protocol"]["network"] = _system.Settings.Network;
            json["protocol"]["validatorscount"] = _system.Settings.ValidatorsCount;
            json["protocol"]["msperblock"] = _system.Settings.MillisecondsPerBlock;
            json["protocol"]["maxtraceableblocks"] = _system.Settings.MaxTraceableBlocks;
            json["protocol"]["maxvaliduntilblockincrement"] = _system.Settings.MaxValidUntilBlockIncrement;
            json["protocol"]["maxtransactionsperblock"] = _system.Settings.MaxTransactionsPerBlock;
            json["protocol"]["memorypoolmaxtransactions"] = _system.Settings.MemoryPoolMaxTransactions;
            json["protocol"]["initialgasdistribution"] = _system.Settings.InitialGasDistribution;
            return json;
        }

        [RpcMethod]
        protected virtual JToken SendRawTransaction(JArray @params)
        {
            Transaction tx = Convert.FromBase64String(@params[0].AsString()).AsSerializable<Transaction>();
            RelayResult reason = _system.Blockchain.Ask<RelayResult>(tx).Result;
            return GetRelayResult(reason.Result, tx.Hash);
        }

        [RpcMethod]
        protected virtual JToken SubmitBlock(JArray @params)
        {
            Block block = Convert.FromBase64String(@params[0].AsString()).AsSerializable<Block>();
            RelayResult reason = _system.Blockchain.Ask<RelayResult>(block).Result;
            return GetRelayResult(reason.Result, block.Hash);
        }
    }
}
