using Google.Protobuf;
using V2Object = Neo.FileStorage.API.Object.Object;
using Neo.FileStorage.Utils;

namespace Neo.FileStorage.Services.Object.Get.Writer
{
    public class SimpleObjectWriter : IHeaderWriter, IChunkWriter
    {
        public V2Object Obj { get; private set; }

        public void WriteHeader(V2Object obj)
        {
            Obj = obj;
        }

        public void WriteChunk(byte[] chunk)
        {
            Obj.Payload = Obj.Payload.Concat(ByteString.CopyFrom(chunk));
        }
    }
}
