using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.Core.Object
{
    public interface IObjectInhumer
    {
        void Inhume(Address tombstone, params Address[] addresses);
    }
}
