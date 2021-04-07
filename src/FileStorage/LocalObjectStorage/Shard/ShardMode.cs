using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.FileStorage.LocalObjectStorage.Shard
{
    public enum ShardMode
    {
        Undefined,
        Active,
        Inactive,
        ReadOnly,
        Fault,
        Evacuate
    }
}
