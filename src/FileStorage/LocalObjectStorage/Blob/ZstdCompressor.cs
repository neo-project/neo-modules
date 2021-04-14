using ZstdNet;

namespace Neo.FileStorage.LocalObjectStorage.Blob
{
    public sealed class ZstdCompressor : ICompressor
    {
        private readonly Compressor compressor = new ();
        private readonly Decompressor decompressor = new ();

        public byte[] Compress(byte[] data)
        {
            return compressor.Wrap(data);
        }

        public byte[] Decompress(byte[] data)
        {
            return decompressor.Unwrap(data);
        }

        public void Dispose()
        {
            compressor.Dispose();
            decompressor.Dispose();
        }
    }
}
