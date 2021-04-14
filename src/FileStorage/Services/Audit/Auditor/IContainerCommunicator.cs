using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using V2Object = Neo.FileStorage.API.Object.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;

namespace Neo.FileStorage.Services.Audit.Auditor
{
    public interface IContainerCommunicator
    {
        StorageGroup GetStorageGroup(AuditTask task, ObjectID oid);
        V2Object GetHeader(AuditTask task, Node node, ObjectID oid, bool relay);
        byte[] GetRangeHash(AuditTask task, Node node, ObjectID oid, Range range);
    }
}
