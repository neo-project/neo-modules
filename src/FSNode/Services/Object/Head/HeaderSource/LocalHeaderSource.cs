using NeoFS.API.v2.Refs;
using V2Object = NeoFS.API.v2.Object.Object;
using Neo.FSNode.LocalObjectStorage.LocalStore;
using System;

namespace Neo.FSNode.Services.Object.Head.HeaderSource
{
    public class LocalHeaderSource : IHeaderSource
    {
        public Storage LocalStorage;

        public V2Object Head(Address address)
        {
            var header = LocalStorage.Head(address);
            if (header is null) throw new InvalidOperationException(nameof(LocalHeaderSource) + " could not find header");
            return header;
        }
    }
}
