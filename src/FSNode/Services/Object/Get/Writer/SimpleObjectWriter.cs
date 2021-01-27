using Google.Protobuf;
using V2Object = NeoFS.API.v2.Object.Object;
using Neo.FSNode.Utils;

namespace Neo.FSNode.Services.Object.Get.Writer
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
