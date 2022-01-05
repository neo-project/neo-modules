using Neo.FileStorage.API.Refs;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Police
{
    public interface IObjectListSource
    {
        List<Address> List(ulong limit);
    }
}
