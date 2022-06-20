using Neo.FileStorage.API.Client;
using Neo.FileStorage.Storage.Services.Reputaion.Common;
using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Reputaion.Local.Remote
{
    public class TrustWriterProvider : IWriterProvider
    {
        public ECDsa Key { get; init; }
        public IFSClient Client { get; init; }

        public IWriter InitWriter(ICommonContext context)
        {
            return new RemoteTrustWriter
            {
                Context = context,
                Key = Key,
                Client = Client,
            };
        }
    }
}
