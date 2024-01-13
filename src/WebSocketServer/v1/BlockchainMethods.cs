// Copyright (C) 2015-2024 The Neo Project.
//
// BlockchainMethods.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
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
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Linq;
using System.Net.WebSockets;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins.WsRpcJsonServer.V1
{
    internal class BlockchainMethods
    {
        private readonly NeoSystem _neoSystem;
        private readonly LocalNode _localNode;

        public BlockchainMethods(
            NeoSystem neoSystem)
        {
            _neoSystem = neoSystem;
            _localNode = _neoSystem.LocalNode.Ask<LocalNode>(new LocalNode.GetInstance()).Result;
            WsRpcJsonServer.RegisterMethods(this);
        }

        [WsRpcJsonMethod]
        public JToken GetBlockHeader(JArray _params)
        {
            if (_params.Count != 1)
                throw new WebSocketException(-32602, "Invalid params");

            Block block;
            if (uint.TryParse(_params[0]?.AsString(), out var blockIndex))
                block = NativeContract.Ledger.GetBlock(_neoSystem.StoreView, blockIndex);
            else if (UInt256.TryParse(_params[0]?.AsString(), out var blockHash))
                block = NativeContract.Ledger.GetBlock(_neoSystem.StoreView, blockHash);
            else
                throw new WebSocketException(-32602, "Invalid params");

            if (block == null)
                throw new WebSocketException(-100, "Unknown block");

            return block.Header.ToJson(_neoSystem.Settings);
        }

        [WsRpcJsonMethod]
        public JToken GetBlock(JArray _params)
        {
            if (_params.Count != 1)
                throw new WebSocketException(-32602, "Invalid params");

            Block block;
            if (uint.TryParse(_params[0]?.AsString(), out var blockIndex))
                block = NativeContract.Ledger.GetBlock(_neoSystem.StoreView, blockIndex);
            else if (UInt256.TryParse(_params[0]?.AsString(), out var blockHash))
                block = NativeContract.Ledger.GetBlock(_neoSystem.StoreView, blockHash);
            else
                throw new WebSocketException(-32602, "Invalid params");

            if (block == null)
                throw new WebSocketException(-100, "Unknown block");

            return block.ToJson(_neoSystem.Settings);
        }

        [WsRpcJsonMethod]
        public JToken GetTransaction(JArray _params)
        {
            if (_params.Count != 1)
                throw new WebSocketException(-32602, "Invalid params");

            Transaction tx;
            if (UInt256.TryParse(_params[0]?.AsString(), out var txHash))
                tx = NativeContract.Ledger.GetTransaction(_neoSystem.StoreView, txHash);
            else
                throw new WebSocketException(-32602, "Invalid params");

            if (tx == null)
                throw new WebSocketException(-100, "Unknown transaction");

            return tx.ToJson(_neoSystem.Settings);
        }

        [WsRpcJsonMethod]
        public JToken GetContract(JArray _params)
        {
            if (_params.Count != 1)
                throw new WebSocketException(-32602, "Invalid params");

            ContractState contractState;
            if (int.TryParse(_params[0]?.AsString(), out var contractId))
                contractState = NativeContract.ContractManagement.GetContractById(_neoSystem.StoreView, contractId);
            else if (UInt160.TryParse(_params[0]?.AsString(), out var scriptHash))
                contractState = NativeContract.ContractManagement.GetContract(_neoSystem.StoreView, scriptHash);
            else
                throw new WebSocketException(-32602, "Invalid params");

            if (contractState == null)
                throw new WebSocketException(-100, "Unknown contract");

            return contractState.ToJson();
        }

        [WsRpcJsonMethod]
        public JToken GetPeers(JArray _params)
        {
            if (_params.Count != 0)
                throw new WebSocketException(-32602, "Invalid params");

            return new JObject()
            {
                ["unconnected"] = new JArray(_localNode.GetUnconnectedPeers().Select(s => new JObject()
                {
                    ["address"] = $"{s.Address}",
                    ["port"] = s.Port,
                })),
                ["connected"] = new JArray(_localNode.GetRemoteNodes().Select(s => new JObject()
                {
                    ["address"] = $"{s.Remote.Address}",
                    ["port"] = s.ListenerTcpPort,
                })),
            };
        }

        [WsRpcJsonMethod]
        public JToken GetVersion(JArray _params)
        {
            if (_params.Count != 0)
                throw new WebSocketException(-32602, "Invalid params");

            return new JObject()
            {
                ["nonce"] = LocalNode.Nonce,
                ["useragent"] = LocalNode.UserAgent,
                ["protocolversion"] = LocalNode.ProtocolVersion,
            };
        }

        [WsRpcJsonMethod]
        public JToken GetProtocolSettings(JArray _params)
        {
            if (_params.Count != 0)
                throw new WebSocketException(-32602, "Invalid params");

            var hardforks = new JObject();
            foreach (var hf in _neoSystem.Settings.Hardforks)
                hardforks.Properties.Add($"{hf.Key}".Replace("HF_", string.Empty).ToLowerInvariant(), hf.Value);

            return new JObject()
            {
                ["addressversion"] = _neoSystem.Settings.AddressVersion,
                ["network"] = _neoSystem.Settings.Network,
                ["validatorscount"] = _neoSystem.Settings.ValidatorsCount,
                ["millisecondsperblock"] = _neoSystem.Settings.MillisecondsPerBlock,
                ["maxtraceableblocks"] = _neoSystem.Settings.MaxTraceableBlocks,
                ["maxvaliduntilblockincrement"] = _neoSystem.Settings.MaxValidUntilBlockIncrement,
                ["maxtransactionsperblock"] = _neoSystem.Settings.MaxTransactionsPerBlock,
                ["memorypoolmaxtransactions"] = _neoSystem.Settings.MemoryPoolMaxTransactions,
                ["initialgasdistribution"] = _neoSystem.Settings.InitialGasDistribution,
                ["hardforks"] = hardforks,
                ["standbycommittee"] = new JArray(_neoSystem.Settings.StandbyCommittee.Select(s => new JString($"{s}"))),
                ["standbyvalidators"] = new JArray(_neoSystem.Settings.StandbyValidators.Select(s => new JString($"{s}"))),
                ["seedlist"] = new JArray(_neoSystem.Settings.SeedList.Select(s => new JString(s))),
            };
        }

        [WsRpcJsonMethod]
        public JToken SendRawTransaction(JArray _params)
        {
            if (_params.Count != 1)
                throw new WebSocketException(-32602, "Invalid params");

            Transaction tx = Convert.FromBase64String(_params[0]!.AsString()).AsSerializable<Transaction>();
            var reason = _neoSystem.Blockchain.Ask<RelayResult>(tx).Result;

            if (reason.Result == VerifyResult.Succeed)
            {
                return new JObject()
                {
                    ["txhash"] = $"{tx.Hash}",
                };
            }
            else
                throw new WebSocketException(-500, $"{reason}");
        }

        [WsRpcJsonMethod]
        public JToken InvokeContract(JArray _params)
        {
            UInt160 scriptHash = UInt160.Zero;
            string methodName = string.Empty;
            ContractParameter[] args = Array.Empty<ContractParameter>();
            Signer[] signers = Array.Empty<Signer>();

            try
            {
                if (_params.Count >= 1 && UInt160.TryParse(_params[0]!.AsString(), out scriptHash))
                {
                    if (_params.Count >= 2)
                    {
                        methodName = _params[1]!.AsString();
                        if (_params.Count >= 3)
                        {
                            args = ((JArray)_params[2]!).Select(s => ContractParameter.FromJson((JObject)s!)).ToArray();
                            if (_params.Count >= 4)
                                signers = ((JArray)_params[3]!).Select(s => Signer.FromJson((JObject)s!)).ToArray();
                        }
                    }
                }
            }
            catch
            {
                throw new WebSocketException(-32602, "Invalid params");
            }

            if (scriptHash == UInt160.Zero || string.IsNullOrEmpty(methodName))
                throw new WebSocketException(-32602, "Invalid params");

            var snapshot = _neoSystem.GetSnapshot();
            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(scriptHash, methodName, args);

            var script = sb.ToArray();
            var tx = signers.Length == 0 ? null : new Transaction()
            {
                Version = 0,
                Nonce = 0,
                ValidUntilBlock = NativeContract.Ledger.CurrentIndex(snapshot) + _neoSystem.Settings.MaxValidUntilBlockIncrement,
                Signers = signers,
                Script = sb.ToArray(),
                Attributes = Array.Empty<TransactionAttribute>(),
            };

            using var engine = ApplicationEngine.Run(script, snapshot, container: tx, settings: _neoSystem.Settings, gas: WsRpcJsonKestrelSettings.Current?.MaxGasInvoke ?? WsRpcJsonKestrelSettings.Default.MaxGasInvoke);

            return new JObject()
            {
                ["state"] = $"{engine.State}",
                ["gasconsumed"] = engine.GasConsumed,
                ["script"] = Convert.ToBase64String(script),
                ["stack"] = new JArray(engine.ResultStack.Select(s => s.ToJson())),
                ["exception"] = engine.FaultException?.InnerException?.Message ?? engine.FaultException?.Message,
                ["notifications"] = new JArray(engine.Notifications.Select(s => s.ToJson(false))),
            };
        }
    }
}
