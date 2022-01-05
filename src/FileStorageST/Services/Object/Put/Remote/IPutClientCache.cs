using System.Collections.Generic;
using Neo.FileStorage.API.Netmap;

namespace Neo.FileStorage.Storage.Services.Object.Put.Remote
{
    public interface IPutClientCache
    {
        IPutClient Get(NodeInfo node);
    }
}
