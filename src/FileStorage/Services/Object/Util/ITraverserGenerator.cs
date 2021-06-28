using Neo.FileStorage.Services.ObjectManager.Placement;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Services.Object.Util
{
    public interface ITraverserGenerator
    {
        Traverser GenerateTraverser(FSAddress address, ulong epoch);
    }
}
