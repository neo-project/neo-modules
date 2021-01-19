using System;
using System.Collections.Generic;

namespace Neo.FSNode.Services.Object.RangeHash
{
    public interface IHasherSource
    {
        void HashRange(RangeHashPrm prm, Action<List<byte[]>> handler);
    }
}