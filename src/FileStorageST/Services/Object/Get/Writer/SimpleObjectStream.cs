using Google.Protobuf;
using Neo.FileStorage.Storage.Utils;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Get.Writer
{
    public class SimpleObjectWriter : IObjectResponseWriter
    {
        public FSObject Object { get; private set; } = new();

        public void WriteHeader(FSObject obj)
        {
            Object = obj;
            Object.Payload = ByteString.Empty;
        }

        public void WriteChunk(byte[] chunk)
        {
            Object.Payload = Object.Payload.Concat(ByteString.CopyFrom(chunk));
        }
    }
}
