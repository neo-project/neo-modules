using Neo.FileStorage.Cache;
using Neo.FileStorage.Services.Reputaion.Common;
using Neo.FileStorage.Services.Reputaion.Local.Storage;

namespace Neo.FileStorage.Services.Reputaion.Local
{
    public class LocalTrustStorage
    {
        public TrustStorage TrustStorage { get; init; }
        public NetmapCache NetmapCache { get; init; }
        public byte[] LocalKey { get; init; }

        public TrustIterator InitIterator(ICommonContext context)
        {
            TrustStorage.DataForEpoch(context.Epoch, out EpochTrustStorage storage);
            return new TrustIterator();
        }
    }
}
