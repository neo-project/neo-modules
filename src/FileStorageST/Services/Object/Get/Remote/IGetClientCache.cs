using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Get.Remote
{
    public interface IGetClientCache
    {
        IGetClient Get(List<Network.Address> addresses);
    }
}
