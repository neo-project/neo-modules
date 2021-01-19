using NeoFS.API.v2.Refs;

namespace Neo.FSNode.Services.ObjectManager.GC
{
    public interface IRemover
    {
        void Delete(Address objects);
    }
}
