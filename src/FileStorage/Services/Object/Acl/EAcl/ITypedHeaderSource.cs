using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Session;
using System.Collections.Generic;

namespace Neo.FileStorage.Services.Object.Acl.EAcl
{
    public interface ITypedHeaderSource
    {
        List<XHeader> HeadersOfSource(HeaderType type);
    }
}
