using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Services.Object.Put;
using System;

namespace Neo.FileStorage.Services.Replicate
{
    public class Configuration
    {
        public static readonly TimeSpan DefaultPutTimeout = TimeSpan.FromSeconds(5);
        public TimeSpan PutTimeout { get; init; } = DefaultPutTimeout;
        public RemoteSender RemoteSender { get; init; }
        public StorageEngine LocalStorage { get; init; }
    }
}
