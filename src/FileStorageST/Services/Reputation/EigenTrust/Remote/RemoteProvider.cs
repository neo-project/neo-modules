using Neo.FileStorage.API.Client;
using Neo.FileStorage.Storage.Services.Reputaion.Common;
using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Remote
{
    public class RemoteProvider : IClientKeyRemoteProvider
    {
        public ECDsa Key { get; init; }

        public IWriterProvider WithClient(IFSClient client)
        {
            return new TrustWriterProvider
            {
                Key = Key,
                Client = client
            };
        }
    }
}
