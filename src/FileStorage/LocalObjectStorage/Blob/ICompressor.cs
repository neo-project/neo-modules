using System;

namespace Neo.FileStorage.LocalObjectStorage.Blob
{
    public interface ICompressor : IDisposable
    {
        byte[] Compress(byte[] data);
        byte[] Decompress(byte[] data);
    }
}
