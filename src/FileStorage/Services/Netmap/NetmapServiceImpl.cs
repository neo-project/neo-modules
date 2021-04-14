using Grpc.Core;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Morph.Invoker;
using System.Threading.Tasks;

namespace Neo.FileStorage.Services.Netmap
{
    public class NetmapServiceImpl : NetmapService.NetmapServiceBase
    {
        private readonly IClient client;
        private readonly StorageNode snode; //TODO: if this the best way?

        /// <summary>
        /// Get NodeInfo structure from the particular node directly. Node information
        /// can be taken from `Netmap` smart contract, but in some cases the one may
        /// want to get recent information directly, or to talk to the node not yet
        /// present in `Network Map` to find out what API version can be used for
        /// further communication. Can also be used to check if node is up and running.
        /// </summary>
        /// <param name="request">The request received from the client.</param>
        /// <param name="context">The context of the server-side call handler being invoked.</param>
        /// <returns>The response to send back to the client (wrapped by a task).</returns>
        public override Task<LocalNodeInfoResponse> LocalNodeInfo(LocalNodeInfoRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                var resp = new LocalNodeInfoResponse()
                {
                    Body = new LocalNodeInfoResponse.Types.Body
                    {
                        Version = Version.SDKVersion(),
                        NodeInfo = snode.LocalNodeInfo,
                    },
                };
                snode.Key.SignResponse(resp);
                return resp;
            });
        }

        /// <summary>
        /// Read recent information about the NeoFS network.
        /// </summary>
        /// <param name="request">The request received from the client.</param>
        /// <param name="context">The context of the server-side call handler being invoked.</param>
        /// <returns>The response to send back to the client (wrapped by a task).</returns>
        public override Task<NetworkInfoResponse> NetworkInfo(NetworkInfoRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                var resp = new NetworkInfoResponse()
                {
                    Body = new NetworkInfoResponse.Types.Body
                    {
                        NetworkInfo = new ()
                        {
                            CurrentEpoch = snode.CurrentEpoch,
                            MagicNumber = snode.ProtocolSettings.Network,
                        }
                    }
                };
                snode.Key.SignResponse(resp);
                return resp;
            });
        }
    }
}
