using System.Security.Cryptography;
using Neo.FileStorage.Services.Reputaion.Common;
using APIClient = Neo.FileStorage.API.Client.Client;

namespace Neo.FileStorage.Services.Reputaion.Local.Remote
{
    public class RemoteProvider : IClientKeyRemoteProvider
    {
        public ECDsa Key { get; init; }

        public IWriterProvider WithClient(APIClient client)
        {
            return new TrustWriterProvider
            {
                Key = Key,
                Client = client
            };
        }
    }
}
