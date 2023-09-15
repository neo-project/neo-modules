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
    partial class NeoService
    {
        [ServiceMethod]
        protected virtual JToken GetConnectionCount(JArray _params)
        {
            return LocalNode.ConnectedCount;
        }

        [ServiceMethod]
        protected virtual JToken GetPeers(JArray _params)
        {
            JObject json = new();
            json["unconnected"] = new JArray(LocalNode.GetUnconnectedPeers().Select(p =>
            {
                JObject peerJson = new();
                peerJson["address"] = p.Address.ToString();
                peerJson["port"] = p.Port;
                return peerJson;
            }));
            json["bad"] = new JArray(); //badpeers has been removed
            json["connected"] = new JArray(LocalNode.GetRemoteNodes().Select(p =>
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
                throw new ServicceException(-500, reason.ToString());
            }
        }

        [ServiceMethod]
        protected virtual JToken GetVersion(JArray _params)
        {
            JObject json = new();
            json["tcpport"] = LocalNode.ListenerTcpPort;
            json["wsport"] = LocalNode.ListenerWsPort;
            json["nonce"] = LocalNode.Nonce;
            json["useragent"] = LocalNode.UserAgent;
            json["protocol"] = new JObject();
            json["protocol"]["addressversion"] = System.Settings.AddressVersion;
            json["protocol"]["network"] = System.Settings.Network;
            json["protocol"]["validatorscount"] = System.Settings.ValidatorsCount;
            json["protocol"]["msperblock"] = System.Settings.MillisecondsPerBlock;
            json["protocol"]["maxtraceableblocks"] = System.Settings.MaxTraceableBlocks;
            json["protocol"]["maxvaliduntilblockincrement"] = System.Settings.MaxValidUntilBlockIncrement;
            json["protocol"]["maxtransactionsperblock"] = System.Settings.MaxTransactionsPerBlock;
            json["protocol"]["memorypoolmaxtransactions"] = System.Settings.MemoryPoolMaxTransactions;
            json["protocol"]["initialgasdistribution"] = System.Settings.InitialGasDistribution;
            return json;
        }

        [ServiceMethod]
        protected virtual JToken SendRawTransaction(JArray _params)
        {
            Transaction tx = Convert.FromBase64String(_params[0].AsString()).AsSerializable<Transaction>();
            RelayResult reason = System.Blockchain.Ask<RelayResult>(tx).Result;
            return GetRelayResult(reason.Result, tx.Hash);
        }

        [ServiceMethod]
        protected virtual JToken SubmitBlock(JArray _params)
        {
            Block block = Convert.FromBase64String(_params[0].AsString()).AsSerializable<Block>();
            RelayResult reason = System.Blockchain.Ask<RelayResult>(block).Result;
            return GetRelayResult(reason.Result, block.Hash);
        }
    }
}
