using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Storage.Services.Reputaion.Common;
using System.Collections.Generic;
using System.Security.Cryptography;
using APITrust = Neo.FileStorage.API.Reputation.Trust;

namespace Neo.FileStorage.Storage.Services.Reputaion.Local.Remote
{
    public class RemoteTrustWriter : IWriter
    {
        public ICommonContext Context { get; init; }
        public ECDsa Key { get; init; }
        public IFSClient Client { get; init; }
        private readonly List<APITrust> buffer = new();

        public void Write(PeerToPeerTrust t)
        {
            buffer.Add(t.Trust);
        }

        public void Close()
        {
            Client.AnnounceTrust(Context.Epoch, buffer, new() { Key = Key });
        }
    }
}
