using System;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Blob
{
    public class BlobException : Exception
    {
        public BlobException(string message) : base(message) { }
    }

    public class BlobFullException : BlobException
    {
        public const string Error = "blob full";

        public BlobFullException() : base(Error) { }

    }
}
