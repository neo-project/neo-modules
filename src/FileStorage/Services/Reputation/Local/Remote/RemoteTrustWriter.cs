using System.Collections.Generic;
using System.Security.Cryptography;
using Google.Protobuf;
using Neo.FileStorage.Services.Reputaion.Common;
using APIClient = Neo.FileStorage.API.Client.Client;
using APITrust = Neo.FileStorage.API.Reputation.Trust;

namespace Neo.FileStorage.Services.Reputaion.Local.Remote
{
    public class RemoteTrustWriter : IWriter
    {
        public ICommonContext Context { get; init; }
        public ECDsa Key { get; init; }
        public APIClient Client { get; init; }
        private readonly List<APITrust> buffer = new();

        public void Write(Trust t)
        {
            APITrust trust = new()
            {
                Peer = new()
                {
                    PublicKey = ByteString.CopyFrom(t.Peer)
                },
                Value = t.Value
            };
            buffer.Add(trust);
        }

        public void Close()
        {
            Client.AnnounceTrust(Context.Epoch, buffer, new() { Key = Key });
        }
    }
}