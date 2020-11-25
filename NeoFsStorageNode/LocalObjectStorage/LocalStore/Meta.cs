using Google.Protobuf;
using NeoFS.API.v2.Refs;
using System;
using System.Linq;
using FsObject = NeoFS.API.v2.Object.Object;

namespace Neo.Fs.LocalObjectStorage.LocalStore
{
    public class ObjectMeta
    {

        private FsObject head; // type to be replaced
        private ulong savedAtEpoch;


        public ulong SaveAtEpoch()
        {
            if (!(this is null))
                return this.savedAtEpoch;
            return 0;
        }

        public FsObject Head()
        {
            if (!(this is null))
                return this.head;
            return null;
        }

        public Address AddressFromMeta()
        {
            if (!(this is null))
                return this.head.Address();
            return null;
        }

        public static ObjectMeta MetaFromObject(FsObject o)
        {
            return new ObjectMeta()
            {
                savedAtEpoch = 10,
                head = o.CutPayload()
            };
        }

        public byte[] MetaToBytes()
        {
            byte[] data = BitConverter.GetBytes(savedAtEpoch);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data);

            data = data.Concat(this.head.ToByteArray()).ToArray(); // TBD
            return data;
        }

        public static ObjectMeta MetaFromBytes(byte[] data)
        {
            if (data.Length < 8)
                throw new ArgumentException("invalid data length");

            var a = data.Take(8).ToArray();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(a);
            var o = FsObject.Parser.ParseFrom(data.Skip(8).ToArray());

            var r = new ObjectMeta()
            {
                head = o,
                savedAtEpoch = BitConverter.ToUInt64(a)
            };
            return r;
        }
    }
}
