using System;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Services.Object.Put;

namespace Neo.FileStorage.Services.Replicate
{
    public class Configuration
    {
        public TimeSpan PutTimeout { get; init; }
        public RemoteSender RemoteSender { get; init; }
        public StorageEngine LocalStorage { get; init; }
    }
}
