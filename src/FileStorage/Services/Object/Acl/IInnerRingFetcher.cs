using System.Collections.Generic;

namespace Neo.FileStorage.Services.Object.Acl
{
    public interface IInnerRingFetcher
    {
        List<byte[]> InnerRingKeys();
    }
}
