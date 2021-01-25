using NeoFS.API.v2.Refs;
using Neo.FSNode.Services.ObjectManager.Placement;

namespace Neo.FSNode.Services.Object.Util
{
    public interface ITraverserGenerator
    {
        Traverser GenerateTraverser(Address address);
    }
}
