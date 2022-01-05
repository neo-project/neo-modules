using Google.Protobuf;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Invoker.Morph;
using System.Linq;

namespace Neo.FileStorage.Storage.Services.Netmap
{
    public class NetmapService
    {
        public IEpochSource EpochSource { get; init; }
        public ILocalInfoSource LocalInfoSource { get; init; }
        public MorphInvoker MorphInvoker { get; init; }

        public LocalNodeInfoResponse LocalNodeInfo(LocalNodeInfoRequest _)
        {
            var resp = new LocalNodeInfoResponse()
            {
                Body = new LocalNodeInfoResponse.Types.Body
                {
                    Version = Version.SDKVersion(),
                    NodeInfo = LocalInfoSource.NodeInfo,
                },
            };
            return resp;
        }

        public NetworkInfoResponse NetworkInfo(NetworkInfoRequest _)
        {
            NetworkInfo networkInfo = new()
            {
                CurrentEpoch = EpochSource.CurrentEpoch,
                MagicNumber = LocalInfoSource.Network,
                MsPerBlock = LocalInfoSource.ProtocolSettings.Network,
                NetworkConfig = new()
            };
            networkInfo.NetworkConfig.Parameters.AddRange(MorphInvoker.ListConfigs().Select(p => new NetworkConfig.Types.Parameter
            {
                Key = ByteString.CopyFrom(p.Item1),
                Value = ByteString.CopyFrom(p.Item2)
            }));
            var resp = new NetworkInfoResponse()
            {
                Body = new NetworkInfoResponse.Types.Body
                {
                    NetworkInfo = networkInfo
                }
            };
            return resp;
        }
    }
}
