using System;

namespace Neo.FileStorage.LocalObjectStorage.Blob
{
    public interface ICompressor
    {
        byte[] Compress(byte[] data);
        byte[] Decompress(byte[] data);
    }
}
