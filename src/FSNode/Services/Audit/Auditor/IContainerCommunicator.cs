using NeoFS.API.v2.Netmap;
using NeoFS.API.v2.Object;
using V2Object = NeoFS.API.v2.Object.Object;
using NeoFS.API.v2.Refs;
using NeoFS.API.v2.StorageGroup;

namespace Neo.FSNode.Services.Audit.Auditor
{
    public interface IContainerCommunicator
    {
        StorageGroup GetStorageGroup(AuditTask task, ObjectID oid);
        V2Object GetHeader(AuditTask task, Node node, ObjectID oid, bool relay);
        byte[] GetRangeHash(AuditTask task, Node node, ObjectID oid, Range range);
    }
}
