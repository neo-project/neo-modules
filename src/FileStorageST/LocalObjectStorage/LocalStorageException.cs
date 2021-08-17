#nullable enable
using System;
using Neo.FileStorage.API.Object;

namespace Neo.FileStorage.Storage.LocalObjectStorage
{
    public class BlobFullException : Exception
    {
        public override string Message => "blob full";

        public BlobFullException() : base() { }
    }
}
