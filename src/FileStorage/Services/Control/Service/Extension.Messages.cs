
using Google.Protobuf;

namespace Neo.FileStorage.Services.Control.Service
{
    public partial class HealthCheckRequest : ISignedMessage
    {
        public IMessage SignedData => Body;
    }

    public partial class HealthCheckResponse : ISignedMessage
    {
        public IMessage SignedData => Body;
    }

    public partial class NetmapSnapshotRequest : ISignedMessage
    {
        public IMessage SignedData => Body;
    }

    public partial class NetmapSnapshotResponse : ISignedMessage
    {
        public IMessage SignedData => Body;
    }

    public partial class SetNetmapStatusRequest : ISignedMessage
    {
        public IMessage SignedData => Body;
    }

    public partial class SetNetmapStatusResponse : ISignedMessage
    {
        public IMessage SignedData => Body;
    }

    public partial class DropObjectsRequest : ISignedMessage
    {
        public IMessage SignedData => Body;
    }

    public partial class DropObjectsResponse : ISignedMessage
    {
        public IMessage SignedData => Body;
    }
}
