using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.Core.Object
{
    public interface IObjectDeleteHandler
    {
        void DeleteObjects(params Address[] addresses);
    }
}
