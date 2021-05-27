using System;
using System.Linq;
using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Blob;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.LocalObjectStorage.Blobstor
{
    public sealed class Blobstorage : IDisposable
    {
        public const ulong DefaultSmallSizeLimit = 1 << 20;
        public const string BlobovniczaDir = "blobovnicza";
        private readonly ICompressor compressor;
        private readonly FSTree fsTree;
        private readonly BlobovniczaTree blobovniczas;
        public ulong SmallSizeLimit { get; init; }

        public Blobstorage()
        {
            SmallSizeLimit = DefaultSmallSizeLimit;
            compressor = new ZstdCompressor();
            fsTree = new();
            blobovniczas = new(BlobovniczaDir, compressor);
        }

        public Blobstorage(FSTree tree) : this()
        {
            fsTree = tree;
        }

        public void Dispose()
        {
            blobovniczas.Dispose();
            compressor.Dispose();
        }

        private bool IsBig(byte[] data)
        {
            return (ulong)data.Length > SmallSizeLimit;
        }

        private bool ExistsBig(Address address)
        {
            try
            {
                _ = fsTree.Exists(address);
                return true;
            }
            catch (ObjectFileNotFoundException)
            {
                return false;
            }
        }

        private bool ExistsSmall(Address address)
        {
            throw new NotImplementedException("neofs-node not implemented");
        }

        public bool Exists(Address address)
        {
            if (ExistsBig(address)) return true;
            return ExistsSmall(address);
        }

        public FSObject GetSmall(Address address, BlobovniczaID id = null)
        {
            return blobovniczas.Get(address, id);
        }

        public FSObject GetBig(Address address)
        {
            byte[] data = fsTree.Get(address);
            data = compressor.Decompress(data);
            return FSObject.Parser.ParseFrom(data);
        }

        public byte[] GetRangeSmall(Address address, FSRange range, BlobovniczaID id = null)
        {
            return blobovniczas.GetRange(address, range, id);
        }

        public byte[] GetRangeBig(Address address, FSRange range)
        {
            FSObject obj = GetBig(address);
            if (obj.PayloadSize < range.Offset + range.Length)
                throw new RangeOutOfBoundsException();
            int start = (int)range.Offset;
            int end = start + (int)range.Length;
            return obj.Payload.ToByteArray()[start..end];
        }

        public BlobovniczaID Put(FSObject obj)
        {
            byte[] data = obj.ToByteArray();
            bool big = IsBig(data);
            if (big)
            {
                data = compressor.Compress(data);
                fsTree.Put(obj.Address, data);
                return null;
            }
            return blobovniczas.Put(obj);
        }

        public void DeleteBig(Address address)
        {
            fsTree.Delete(address);
        }

        public void DeleteSmall(Address address, BlobovniczaID id = null)
        {
            blobovniczas.Delete(address, id);
        }

        public void Iterate()
        {
            throw new NotImplementedException("neofs-node not implemented");
        }
    }
}
