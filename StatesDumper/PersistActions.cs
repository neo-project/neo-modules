using System;

namespace Cron.Plugins
{
    [Flags]
    internal enum PersistActions : byte
    {
        StorageChanges = 0b00000001
    }
}
