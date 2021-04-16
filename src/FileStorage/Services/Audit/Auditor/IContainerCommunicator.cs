using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using FSObject = Neo.FileStorage.API.Object.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;

namespace Neo.FileStorage.Services.Audit.Auditor
{
    public interface IContainerCommunicator
    {
        StorageGroup GetStorageGroup(AuditTask task, ObjectID oid);
        FSObject GetHeader(AuditTask task, Node node, ObjectID oid, bool relay);
        byte[] GetRangeHash(AuditTask task, Node node, ObjectID oid, Range range);
    }
}
