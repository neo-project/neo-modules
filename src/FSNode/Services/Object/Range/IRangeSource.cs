using V2Range = NeoFS.API.v2.Object.Range;
using NeoFS.API.v2.Refs;

namespace Neo.FSNode.Services.Object.Range
{
    public interface IRangeSource
    {
        byte[] Range(Address address, V2Range range);
    }
}
