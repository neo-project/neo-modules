using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Blob;
using Neo.IO.Data.LevelDB;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.LocalObjectStorage.MetaBase
{
    public sealed partial class MB
    {
        public List<Address> Select(ContainerID cid, SearchFilters filters)
        {
            throw new NotImplementedException();
        }
    }
}
