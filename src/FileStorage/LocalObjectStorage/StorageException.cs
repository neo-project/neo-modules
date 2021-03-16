
using System;
using Neo.FileStorage.API.Object;

namespace Neo.FileStorage.LocalObjectStorage
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
