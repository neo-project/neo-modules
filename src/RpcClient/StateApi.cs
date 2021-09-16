using System;
using System.Linq;
using System.Threading.Tasks;
using Neo.IO.Json;
using Neo.Network.RPC.Models;

namespace Neo.Network.RPC
{
    public class StateApi : ContractClient
    {
        public StateApi(RpcClient rpc) : base(rpc) { }

        public async Task<RpcStateRoot> GetStateRootAsync(uint index)
        {
            var result = await rpcClient.RpcSendAsync(RpcClient.GetRpcName(), index).ConfigureAwait(false);
            return RpcStateRoot.FromJson(result);
        }

        public async Task<byte[]> GetProofAsync(UInt256 rootHash, UInt160 scriptHash, byte[] key)
        {
            var result = await rpcClient.RpcSendAsync(RpcClient.GetRpcName(),
                rootHash.ToString(), scriptHash.ToString(), Convert.ToBase64String(key)).ConfigureAwait(false);
            return Convert.FromBase64String(result.AsString());
        }

        public async Task<byte[]> VerifyProofAsync(UInt256 rootHash, byte[] proofBytes)
        {
            var result = await rpcClient.RpcSendAsync(RpcClient.GetRpcName(),
                rootHash.ToString(), Convert.ToBase64String(proofBytes)).ConfigureAwait(false);

            return Convert.FromBase64String(result.AsString());
        }

        public async Task<(uint? localRootIndex, uint? validatedRootIndex)> GetStateHeightAsync()
        {
            var result = await rpcClient.RpcSendAsync(RpcClient.GetRpcName()).ConfigureAwait(false);
            var localRootIndex = ToNullableUint(result["localrootindex"]);
            var validatedRootIndex = ToNullableUint(result["validatedrootindex"]);
            return (localRootIndex, validatedRootIndex);
        }

        static uint? ToNullableUint(JObject json) => (json == null) ? (uint?)null : (uint?)json.AsNumber();

        public static JObject[] MakeFindStatesParams(UInt256 rootHash, UInt160 scriptHash, byte[] prefix, byte[] from = null, int? count = null)
        {
            var paramCount = from == null ? 3 : count == null ? 4 : 5;
            var @params = new JObject[paramCount];
            @params[0] = rootHash.ToString();
            @params[1] = scriptHash.ToString();
            @params[2] = Convert.ToBase64String(prefix);
            if (from != null)
            {
                @params[3] = Convert.ToBase64String(from);
                if (count.HasValue)
                {
                    @params[4] = count.Value;
                }
            }
            return @params;
        }

        public async Task<(byte[] key, byte[] value)[]> FindStatesAsync(UInt256 rootHash, UInt160 scriptHash, byte[] prefix, byte[] from = null, int? count = null)
        {
            var @params = MakeFindStatesParams(rootHash, scriptHash, prefix, from, count);
            var result = (JArray)await rpcClient.RpcSendAsync(RpcClient.GetRpcName(), @params).ConfigureAwait(false);
            return result.Select(j => (
                    Convert.FromBase64String(j["key"].AsString()),
                    Convert.FromBase64String(j["value"].AsString())
                )).ToArray();
        }

        public async Task<byte[]> GetStateAsync(UInt256 rootHash, UInt160 scriptHash, byte[] key)
        {
            var result = await rpcClient.RpcSendAsync(RpcClient.GetRpcName(),
                rootHash.ToString(), scriptHash.ToString(), Convert.ToBase64String(key)).ConfigureAwait(false);
            return Convert.FromBase64String(result.AsString());
        }
    }
}
