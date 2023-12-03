using Akka.Actor;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.Linq;
using System.Net.WebSockets;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins.WebSocketServer.v1
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
            WebSocketServerPlugin.RegisterMethods(this);
        }

        [WebSocketMethod]
        public JToken GetBlockHeader(JArray _params)
        {
            if (_params.Count != 1)
                throw new WebSocketException(-32602, "Invalid params");

            Block block;
            if (uint.TryParse(_params[0].AsString(), out var blockIndex))
                block = NativeContract.Ledger.GetBlock(_neoSystem.StoreView, blockIndex);
            else if (UInt256.TryParse(_params[0].AsString(), out var blockHash))
                block = NativeContract.Ledger.GetBlock(_neoSystem.StoreView, blockHash);
            else
                throw new WebSocketException(-32602, "Invalid params");

            if (block == null)
                throw new WebSocketException(-100, "Unknown block");

            return block.Header.ToJson(_neoSystem.Settings);
        }

        [WebSocketMethod]
        public JToken GetBlock(JArray _params)
        {
            if (_params.Count != 1)
                throw new WebSocketException(-32602, "Invalid params");

            Block block;
            if (uint.TryParse(_params[0].AsString(), out var blockIndex))
                block = NativeContract.Ledger.GetBlock(_neoSystem.StoreView, blockIndex);
            else if (UInt256.TryParse(_params[0].AsString(), out var blockHash))
                block = NativeContract.Ledger.GetBlock(_neoSystem.StoreView, blockHash);
            else
                throw new WebSocketException(-32602, "Invalid params");

            if (block == null)
                throw new WebSocketException(-100, "Unknown block");

            return block.ToJson(_neoSystem.Settings);
        }

        [WebSocketMethod]
        public JToken GetTransaction(JArray _params)
        {
            if (_params.Count != 1)
                throw new WebSocketException(-32602, "Invalid params");

            Transaction tx;
            if (UInt256.TryParse(_params[0].AsString(), out var txHash))
                tx = NativeContract.Ledger.GetTransaction(_neoSystem.StoreView, txHash);
            else
                throw new WebSocketException(-32602, "Invalid params");

            if (tx == null)
                throw new WebSocketException(-100, "Unknown transaction");

            return tx.ToJson(_neoSystem.Settings);
        }

        [WebSocketMethod]
        public JToken GetContract(JArray _params)
        {
            if (_params.Count != 1)
                throw new WebSocketException(-32602, "Invalid params");

            ContractState contractState;
            if (int.TryParse(_params[0].AsString(), out var contractId))
                contractState = NativeContract.ContractManagement.GetContractById(_neoSystem.StoreView, contractId);
            else if (UInt160.TryParse(_params[0].AsString(), out var scriptHash))
                contractState = NativeContract.ContractManagement.GetContract(_neoSystem.StoreView, scriptHash);
            else
                throw new WebSocketException(-32602, "Invalid params");

            if (contractState == null)
                throw new WebSocketException(-100, "Unknown contract");

            return contractState.ToJson();
        }

        [WebSocketMethod]
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

        [WebSocketMethod]
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

        [WebSocketMethod]
        public JToken GetProtocolSettings(JArray _params)
        {
            if (_params.Count != 0)
                throw new WebSocketException(-32602, "Invalid params");

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
                ["hardforks"] = new JArray(_neoSystem.Settings.Hardforks.Select(s => new JObject()
                {
                    ["name"] = $"{s.Key}".Replace("HF_", string.Empty),
                    ["blockheight"] = s.Value,
                })),
                ["standbycommittee"] = new JArray(_neoSystem.Settings.StandbyCommittee.Select(s => new JString($"{s}"))),
                ["seedlist"] = new JArray(_neoSystem.Settings.SeedList.Select(s => new JString(s))),
            };
        }

        [WebSocketMethod]
        public JToken SendRawTransaction(JArray _params)
        {
            if (_params.Count != 1)
                throw new WebSocketException(-32602, "Invalid params");

            Transaction tx = Convert.FromBase64String(_params[0].AsString()).AsSerializable<Transaction>();
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
    }
}
