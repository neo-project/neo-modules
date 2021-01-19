using System.Collections.Generic;

namespace Neo.FSNode.Services.Object.Acl
{
    public interface IInnerRingFetcher
    {
        List<byte[]> InnerRingKeys();
    }
}
