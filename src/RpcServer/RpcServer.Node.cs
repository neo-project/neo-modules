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
        protected virtual JToken GetConnectionCount(JArray _params)
        {
            return localNode.ConnectedCount;
        }

        [RpcMethod]
        protected virtual JToken GetPeers(JArray _params)
        {
            JObject json = new();
            json["unconnected"] = new JArray(localNode.GetUnconnectedPeers().Select(p =>
            {
                JObject peerJson = new();
                peerJson["address"] = p.Address.ToString();
                peerJson["port"] = p.Port;
                return peerJson;
            }));
            json["bad"] = new JArray(); //badpeers has been removed
            json["connected"] = new JArray(localNode.GetRemoteNodes().Select(p =>
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

            switch (reason)
            {
                case VerifyResult.Succeed:
                    {
                        var ret = new JObject();
                        ret["hash"] = hash.ToString();
                        return ret;
                    }
                case VerifyResult.AlreadyExists:
                    {
                        throw new RpcException(RpcError.AlreadyExists.WithData(reason.ToString()));
                    }
                case VerifyResult.OutOfMemory:
                    {
                        throw new RpcException(RpcError.MempoolCapReached.WithData(reason.ToString()));
                    }
                case VerifyResult.InvalidScript:
                    {
                        throw new RpcException(RpcError.InvalidScript.WithData(reason.ToString()));
                    }
                case VerifyResult.InvalidAttribute:
                    {
                        throw new RpcException(RpcError.InvalidAttribute.WithData(reason.ToString()));
                    }
                case VerifyResult.InvalidSignature:
                    {
                        throw new RpcException(RpcError.InvalidSignature.WithData(reason.ToString()));
                    }
                case VerifyResult.OverSize:
                    {
                        throw new RpcException(RpcError.InvalidSize.WithData(reason.ToString()));
                    }
                case VerifyResult.Expired:
                    {
                        throw new RpcException(RpcError.ExpiredTransaction.WithData(reason.ToString()));
                    }
                case VerifyResult.InsufficientFunds:
                    {
                        throw new RpcException(RpcError.InsufficientFunds.WithData(reason.ToString()));
                    }
                case VerifyResult.PolicyFail:
                    {
                        throw new RpcException(RpcError.PolicyFailed.WithData(reason.ToString()));
                    }
                default:
                    {
                        throw new RpcException(RpcError.VerificationFailed.WithData(reason.ToString()));
                    }
            }
        }

        [RpcMethod]
        protected virtual JToken GetVersion(JArray _params)
        {
            JObject json = new();
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
            json["protocol"]["hardforks"] = new JArray(system.Settings.Hardforks.Select(hf =>
            {
                JObject forkJson = new();
                // Strip "HF_" prefix.
                forkJson["name"] = StripPrefix(hf.Key.ToString(), "HF_");
                forkJson["blockheight"] = hf.Value;
                return forkJson;
            }));
            return json;
        }

        private static string StripPrefix(string s, string prefix)
        {
            return s.StartsWith(prefix) ? s.Substring(prefix.Length) : s;
        }

        [RpcMethod]
        protected virtual JToken SendRawTransaction(JArray _params)
        {
            Transaction tx = Result.Ok_Or(() => Convert.FromBase64String(_params[0].AsString()).AsSerializable<Transaction>(), RpcError.InvalidParams.WithData($"Invalid Transaction Format: {_params[0]}"));
            RelayResult reason = system.Blockchain.Ask<RelayResult>(tx).Result;
            return GetRelayResult(reason.Result, tx.Hash);
        }

        [RpcMethod]
        protected virtual JToken SubmitBlock(JArray _params)
        {
            Block block = Result.Ok_Or(() => Convert.FromBase64String(_params[0].AsString()).AsSerializable<Block>(), RpcError.InvalidParams.WithData($"Invalid Block Format: {_params[0]}"));
            RelayResult reason = system.Blockchain.Ask<RelayResult>(block).Result;
            return GetRelayResult(reason.Result, block.Hash);
        }
    }
}
