using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Session;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Acl.EAcl
{
    public interface IHeaderSource
    {
        IEnumerable<XHeader> HeadersOfType(HeaderType type);
    }
}
