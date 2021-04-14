using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Get
{
    public interface IObjectWriter
    {
        void WriteHeader(FSObject obj);
        void WriteChunk(byte[] chunk);
    }
}
