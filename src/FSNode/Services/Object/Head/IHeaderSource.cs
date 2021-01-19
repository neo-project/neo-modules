using NeoFS.API.v2.Refs;
using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.Services.Object.Head
{
    public interface IHeaderSource
    {
        V2Object Head(Address address);
    }
}
