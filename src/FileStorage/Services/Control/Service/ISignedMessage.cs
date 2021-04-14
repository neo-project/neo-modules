
using Google.Protobuf;

namespace Neo.FileStorage.Services.Control.Service
{
    public interface ISignedMessage
    {
        IMessage SignedData { get; }
        Signature Signature { get; set; }
    }
}
