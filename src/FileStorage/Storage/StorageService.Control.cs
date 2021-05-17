using System;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Services.Control;

namespace Neo.FileStorage
{
    public sealed partial class StorageService : IDisposable
    {
        private ControlServiceImpl InitializeControl(StorageEngine localStorage)
        {
            return new ControlServiceImpl
            {
                Key = key,
                LocalStorage = localStorage,
                MorphClient = morphClient,
                StorageNode = this,
            };
        }
    }
}
