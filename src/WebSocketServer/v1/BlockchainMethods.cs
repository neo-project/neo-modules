using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System.Net.WebSockets;

namespace Neo.Plugins.WebSocketServer.v1
{
    internal class BlockchainMethods
    {
        private readonly NeoSystem _neoSystem;

        public BlockchainMethods(
            NeoSystem neoSystem)
        {
            _neoSystem = neoSystem;
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
    }
}
