using Google.Protobuf;
using Neo.FileStorage.Storage.Services.Control;
using System;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IDisposable
    {
        private ControlServiceImpl InitializeControl()
        {
            var controlService = new ControlServiceImpl
            {
                Key = key,
                LocalStorage = localStorage,
                EpochSource = this,
                NetmapSource = netmapCache,
                StorageService = this,
            };
            foreach (var key in Settings.Default.Administrators)
                controlService.AllowKeys.Add(ByteString.CopyFrom(key));
            return controlService;
        }
    }
}
