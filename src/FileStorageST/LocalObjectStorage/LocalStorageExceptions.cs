using System;

namespace Neo.FileStorage.Storage.LocalObjectStorage
{
    public class BlobFullException : Exception
    {
        public override string Message => "blob full";

        public BlobFullException() : base() { }
    }
}
