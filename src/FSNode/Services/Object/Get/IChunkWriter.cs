
namespace Neo.FSNode.Services.Object.Get
{
    public interface IChunkWriter
    {
        void WriteChunk(byte[] chunk);
    }
}
