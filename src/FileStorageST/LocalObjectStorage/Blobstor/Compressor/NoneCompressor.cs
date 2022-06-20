namespace Neo.FileStorage.Storage.LocalObjectStorage.Blobstor
{
    public sealed class NoneCompressor : ICompressor
    {
        public byte[] Compress(byte[] data)
        {
            return data;
        }
        public byte[] Decompress(byte[] data)
        {
            return data;
        }

        public bool IsCompressed(byte[] data)
        {
            return false;
        }
    }
}
