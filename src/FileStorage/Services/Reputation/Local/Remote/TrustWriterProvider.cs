using System.Security.Cryptography;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.Services.Reputaion.Common;

namespace Neo.FileStorage.Services.Reputaion.Local.Remote
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
