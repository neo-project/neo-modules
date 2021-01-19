using V2Range = NeoFS.API.v2.Object.Range;
using NeoFS.API.v2.Refs;
using Neo.FSNode.LocalObjectStorage.LocalStore;
using System;

namespace Neo.FSNode.Services.Object.Range.RangeSource
{
    public class LocalRangeSource : IRangeSource
    {
        private Storage localStorage;

        public byte[] Range(Address address, V2Range range)
        {
            var obj = localStorage.Get(address);
            if (obj is null)
                throw new InvalidOperationException(nameof(Range) + " could not get object from local storage");
            var payload = obj.Payload;
            var start = (int)range.Offset;
            var end = (int)(range.Offset + range.Length);
            return payload.ToByteArray()[start..end];
        }
    }
}
