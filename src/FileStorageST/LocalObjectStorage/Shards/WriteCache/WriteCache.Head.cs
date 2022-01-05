using Neo.FileStorage.API.Refs;
using System;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public sealed partial class WriteCache : IDisposable
    {
        public FSObject Head(Address address)
        {
            return Get(address).CutPayload();
        }
    }
}
