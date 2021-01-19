using Google.Protobuf;
using Google.Protobuf.Collections;
using NeoFS.API.v2.Refs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.FSNode.Core.Object
{
    public class Tombstone
    {
        public List<Address> Addresses;

        public byte[] ToByteArray()
        {
            if (Addresses is null) throw new InvalidOperationException(nameof(Tombstone) + " tombstone should not be empty");
            var repeated = new RepeatedField<Address>();
            repeated.AddRange(Addresses);
            using MemoryStream ms = new MemoryStream();
            CodedOutputStream output = new CodedOutputStream(ms);
            repeated.WriteTo(output, FieldCodec.ForMessage(10, Address.Parser));
            output.Flush();
            return ms.ToArray();
        }

        public ByteString ToByteString()
        {
            return ByteString.CopyFrom(ToByteArray());
        }

        public static Tombstone FromByteArray(byte[] bytes)
        {
            CodedInputStream input = new CodedInputStream(bytes);
            var tag = input.ReadTag();
            if (tag != 10) throw new FormatException(nameof(Tombstone) + " invalid bytes");
            var repeated = new RepeatedField<Address>();
            repeated.AddEntriesFrom(input, FieldCodec.ForMessage(10, Address.Parser));
            return new Tombstone
            {
                Addresses = repeated.ToList(),
            };
        }

        public static Tombstone FromByteString(ByteString bs)
        {
            return FromByteArray(bs.ToByteArray());
        }
    }
}
