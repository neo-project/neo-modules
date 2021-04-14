using Google.Protobuf;
using Neo.FileStorage.API.Refs;
using Neo.IO.Data.LevelDB;
using System;
using System.Text;
using System.Threading;
using FSRange = Neo.FileStorage.API.Object.Range;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.LocalObjectStorage.Blob
{
    public sealed class Blobovnicza : IEquatable<Blobovnicza>, IDisposable
    {
        public const long DefaultFullSizeLimit = 1 << 30;
        public const long DefaultObjSizeLimit = 1 << 20;
        public readonly Func<byte[], byte[]> DefaultCompressor = d => d;
        public string Path { get; private set; }
        public ICompressor Compressor { get; init; }
        public long FullSizeLimit { get; init; }
        public long ObjSizeLimit { get; init; }
        private DB dB;
        private long filled;

        public Blobovnicza(string path)
        {
            Path = path;
            FullSizeLimit = DefaultFullSizeLimit;
            ObjSizeLimit = DefaultObjSizeLimit;
            Compressor = new NoneCompressor();
        }

        public void Open()
        {
            dB = DB.Open(Path);
        }

        public void Dispose()
        {
            dB.Dispose();
        }

        private byte[] Addresskey(Address address)
        {
            return Encoding.UTF8.GetBytes(address.String());
        }

        public FSObject Get(Address address)
        {
            if (address is null)
                throw new ArgumentNullException(nameof(address));
            var raw = dB.Get(ReadOptions.Default, Addresskey(address));
            if (raw is null) throw new ObjectNotFoundException();
            return FSObject.Parser.ParseFrom(Compressor.Decompress(raw));
        }

        public byte[] GetRange(Address address, FSRange range)
        {
            var obj = Get(address);
            var from = (int)range.Offset;
            var to = from + (int)range.Length;
            if (to < from)
                throw new ArgumentException("invalid range");
            else if (obj.Payload.Length < to)
                throw new RangeOutOfBoundsException();
            return obj.Payload.ToByteArray()[from..to];
        }

        public void Put(FSObject obj)
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (FullSizeLimit < filled) throw new BlobFullException();
            var raw = Compressor.Compress(obj.ToByteArray());
            if (ObjSizeLimit < raw.Length)
                throw new ObjectSizeExceedLimitException();
            dB.Put(WriteOptions.Default, Addresskey(obj.Address), raw);
            IncSize(raw.Length);
        }

        public void Delete(Address address)
        {
            if (address is null)
                throw new ArgumentNullException(nameof(address));
            var raw = dB.Get(ReadOptions.Default, Addresskey(address));
            if (raw is null) throw new ObjectNotFoundException();
            dB.Delete(WriteOptions.Default, Addresskey(address));
            DecSize(raw.Length);
        }

        private void IncSize(long size)
        {
            Interlocked.Add(ref filled, size);
        }

        private void DecSize(long size)
        {
            Interlocked.Add(ref filled, -size);
        }

        public bool Equals(Blobovnicza other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (Path == other.Path) return true;
            return false;
        }
    }
}
