
namespace Neo.FileStorage.Services.Object.Get
{
    public interface IChunkWriter
    {
        void WriteChunk(byte[] chunk);
    }
}
