namespace Neo.FileStorage.Storage.LocalObjectStorage.Blob
{
    public sealed class CompressorWrapper : ICompressor
    {
        private readonly ICompressor compressor;

        public CompressorWrapper(ICompressor c)
        {
            compressor = c;
        }

        public byte[] Compress(byte[] data)
        {
            return compressor.Compress(data);
        }

        public byte[] Decompress(byte[] data)
        {
            if (compressor.IsCompressed(data))
                return compressor.Decompress(data);
            return data;
        }

        public bool IsCompressed(byte[] data)
        {
            return compressor.IsCompressed(data);
        }
    }
}
