using System;
using static Neo.FileStorage.Tests.LocalObjectStorage.Helper;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Tests.Services.Container
{
    public static class Helper
    {
        public static ulong RandomUInt64(ulong max = ulong.MaxValue)
        {
            var random = new Random();
            var buffer = new byte[sizeof(ulong)];
            random.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer) % max;
        }

        public static FSAnnouncement RandomAnnouncement()
        {
            return new()
            {
                ContainerId = RandomContainerID(),
                UsedSpace = RandomUInt64(),
            };
        }
    }
}
