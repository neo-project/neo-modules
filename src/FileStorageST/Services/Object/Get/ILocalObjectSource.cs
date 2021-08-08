using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.Services.Object.Get
{
    public interface ILocalObjectSource
    {
        API.Object.Object Get(Address address);
        API.Object.Object Head(Address address, bool raw);
        API.Object.Object GetRange(Address address, Range range);
    }
}
