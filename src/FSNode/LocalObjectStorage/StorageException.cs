
using System;
using NeoFS.API.v2.Object;

namespace Neo.FSNode.LocalObjectStorage
{
    public class LocalStorageException : Exception
    {

    }

    public class ObjectAlreadyRemovedException : LocalStorageException
    {

    }

    public class SplitInfoException : LocalStorageException
    {
        public SplitInfo SplitInfo;
    }

    public class RangeOutOfBoundsException : LocalStorageException
    {

    }
}
