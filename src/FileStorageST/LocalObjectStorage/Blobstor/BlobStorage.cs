using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.LocalObjectStorage.Blob;
using System;
using ZstdNet;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Blobstor
{
    public sealed class BlobStorage : IDisposable
    {
        public const ulong DefaultSmallSizeLimit = 1 << 20;
        public const string BlobovniczasDir = "Data_Blobovniczas";
        private readonly ICompressor compressor;
        private readonly FSTree fsTree;
        private readonly BlobovniczaTree blobovniczas;
        private readonly ulong smallSizeLimit;
        private readonly string[] CompressExcludeContentTypes;

        public BlobStorage(BlobStorageSettings settings)
        {
            smallSizeLimit = settings.SmallSizeLimit;
            compressor = new CompressorWrapper(settings.Compress ? new ZstdCompressor() : new NoneCompressor());
            CompressExcludeContentTypes = settings.CompressExcludeContentTypes;
            fsTree = new(System.IO.Path.Join(settings.Path, FSTree.DefaultPath), settings.FSTreeSettings.ShallowDepth, settings.FSTreeSettings.DirectoryNameLength);
            blobovniczas = new()
            {
                BlzRootPath = System.IO.Path.Join(settings.Path, BlobovniczasDir),
                Compressor = compressor,
                BlzShallowDepth = (ulong)settings.BlobovniczaSettings.ShallowDepth,
                BlzShallowWidth = (ulong)settings.BlobovniczaSettings.ShallowWidth
            };
        }

        public void Open()
        {
            blobovniczas.Open();
        }

        public void Dispose()
        {
            blobovniczas?.Dispose();
        }

        public bool NeedsToCompress(FSObject obj)
        {
            if (compressor is null) return false;
            if (CompressExcludeContentTypes.Length == 0) return compressor is not null;
            foreach (var attr in obj.Attributes)
            {
                if (attr.Key == Header.Types.Attribute.AttributeContentType)
                {
                    foreach (var t in CompressExcludeContentTypes)
                    {
                        if (t == "*") return false;
                        if (t.Length > 0 && t[^1] == '*' && attr.Value.StartsWith(t[..^1])) return false;
                        if (t.Length > 0 && t[0] == '*' && attr.Value.EndsWith(t[1..])) return false;
                        if (t.Length > 0 && t == attr.Value) return false;
                    }
                }
            }
            return true;
        }

        private bool IsBig(byte[] data)
        {
            return (ulong)data.Length > smallSizeLimit;
        }

        private bool ExistsBig(Address address)
        {
            try
            {
                _ = fsTree.Exists(address);
                return true;
            }
            catch (ObjectNotFoundException)
            {
                return false;
            }
        }

        public FSObject GetSmall(Address address, BlobovniczaID id = null)
        {
            return FSObject.Parser.ParseFrom(blobovniczas.Get(address, id));
        }

        public FSObject GetBig(Address address)
        {
            byte[] data = fsTree.Get(address);
            data = compressor.Decompress(data);
            return FSObject.Parser.ParseFrom(data);
        }

        public byte[] GetRangeSmall(Address address, FSRange range, BlobovniczaID id = null)
        {
            var obj = GetSmall(address, id);
            if (obj.PayloadSize < range.Offset + range.Length)
                throw new RangeOutOfBoundsException();
            int start = (int)range.Offset;
            int end = start + (int)range.Length;
            return obj.Payload.ToByteArray()[start..end];
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
            return PutRaw(obj.Address, data, NeedsToCompress(obj));
        }

        public BlobovniczaID PutRaw(Address address, byte[] data, bool compress)
        {
            var isBig = IsBig(data);
            if (compress)
                data = compressor.Compress(data);
            if (isBig)
            {
                fsTree.Put(address, data);
                return null;
            }
            return blobovniczas.Put(address, data);
        }

        public void DeleteBig(Address address)
        {
            fsTree.Delete(address);
        }

        public void DeleteSmall(Address address, BlobovniczaID id = null)
        {
            blobovniczas.Delete(address, id);
        }
    }
}
