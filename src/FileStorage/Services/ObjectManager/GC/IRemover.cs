using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Services.ObjectManager.GC
{
    public interface IRemover
    {
        void Delete(Address objects);
    }
}
