using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.ObjectManager.Placement;

namespace Neo.FileStorage.Services.Object.Util
{
    public interface ITraverserGenerator
    {
        Traverser GenerateTraverser(Address address);
    }
}
