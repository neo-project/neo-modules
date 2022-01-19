namespace Neo.FileStorage.Storage.LocalObjectStorage.Blobstor
{
    public interface ICompressor
    {
        byte[] Compress(byte[] data);
        byte[] Decompress(byte[] data);
        bool IsCompressed(byte[] data);
    }
}
