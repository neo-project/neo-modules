using System.Security.Cryptography;
using Neo.FileStorage.Services.Reputaion.Common;
using APIClient = Neo.FileStorage.API.Client.Client;

namespace Neo.FileStorage.Services.Reputaion.Local.Remote
{
    public class TrustWriterProvider : IWriterProvider
    {
        public ECDsa Key { get; init; }
        public APIClient Client { get; init; }

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
