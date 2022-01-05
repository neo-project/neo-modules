using System.Collections.Generic;
using Neo.FileStorage.API.Netmap;

namespace Neo.FileStorage.Storage.Services.Object.Get.Remote
{
    public interface IGetClientCache
    {
        IGetClient Get(NodeInfo node);
    }
}
