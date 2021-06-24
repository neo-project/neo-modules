using System.Collections.Generic;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Session;

namespace Neo.FileStorage.Services.Object.Acl.EAcl
{
    public interface IHeaderSource
    {
        IEnumerable<XHeader> HeadersOfType(HeaderType type);
    }
}
