using System.Collections.Generic;
using Neo.FileStorage.API.Client;

namespace Neo.FileStorage.Storage.Services.Object.Get.Remote
{
    public interface IGetClientCache
    {
        IGetClient Get(List<Network.Address> address);
    }
}
