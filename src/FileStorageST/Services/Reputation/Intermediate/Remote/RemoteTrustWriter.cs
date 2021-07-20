using System.Collections.Generic;
using System.Security.Cryptography;
using Google.Protobuf;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Storage.Services.Reputaion.Common;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust;

namespace Neo.FileStorage.Storage.Services.Reputaion.Intermediate.Remote
{
    public class RemoteTrustWriter : IWriter
    {
        public IterationContext Context { get; init; }
        public ECDsa Key { get; init; }
        public IFSClient Client { get; init; }
        private readonly List<PeerToPeerTrust> buffer = new();

        public void Write(PeerToPeerTrust t)
        {
            buffer.Add(t);
        }

        public void Close()
        {
            foreach (var t in buffer)
                Client.AnnounceIntermediateTrust(Context.Epoch, Context.Index, t, new() { Key = Key });
        }
    }
}
