using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage;

namespace Neo.FileStorage.Storage.Services.Netmap
{
    public class NetmapService
    {
        public IEpochSource EpochSource { get; init; }
        public ILocalInfoSource LocalInfoSource { get; init; }

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
            var resp = new NetworkInfoResponse()
            {
                Body = new NetworkInfoResponse.Types.Body
                {
                    NetworkInfo = new()
                    {
                        CurrentEpoch = EpochSource.CurrentEpoch,
                        MagicNumber = LocalInfoSource.Network,
                    }
                }
            };
            return resp;
        }
    }
}
