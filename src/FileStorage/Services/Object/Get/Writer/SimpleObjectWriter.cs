using Google.Protobuf;
using Neo.FileStorage.Utils;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Get.Writer
{
    public class SimpleObjectWriter : IObjectWriter
    {
        public FSObject Obj { get; private set; }

        public void WriteHeader(FSObject obj)
        {
            Obj = obj;
        }

        public void WriteChunk(byte[] chunk)
        {
            Obj.Payload = Obj.Payload.Concat(ByteString.CopyFrom(chunk));
        }
    }
}
