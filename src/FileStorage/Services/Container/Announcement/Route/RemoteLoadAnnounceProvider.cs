
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Network;
using Neo.FileStorage.Network.Cache;
using Neo.FileStorage.Services.Container.Announcement.Storage;
using System;
using System.Security.Cryptography;

namespace Neo.FileStorage.Services.Container.Announcement.Route
{
    public class RemoteLoadAnnounceProvider
    {
        private readonly ECDsa key;
        private readonly ILocalAddressSource localAddressSource;
        private readonly ClientCache clientCache;
        private readonly AnnouncementStorage deadEndProvider;

        public RemoteLoadAnnounceWriterProvider InitRemote(NodeInfo info)
        {
            throw new NotImplementedException();
        }
    }
}
