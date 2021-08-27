using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Put.Remote
{
    public interface IPutClientCache
    {
        IPutClient Get(List<Network.Address> address);
    }
}
