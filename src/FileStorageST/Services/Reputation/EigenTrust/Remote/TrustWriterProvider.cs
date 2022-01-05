using Neo.FileStorage.API.Client;
using Neo.FileStorage.Storage.Services.Reputaion.Common;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust;
using System;
using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Remote
{
    public class TrustWriterProvider : IWriterProvider
    {
        public ECDsa Key { get; init; }
        public IFSClient Client { get; init; }

        public IWriter InitWriter(ICommonContext context)
        {
            if (context is IterationContext ictx)
                return new RemoteTrustWriter
                {
                    Context = ictx,
                    Key = Key,
                    Client = Client,
                };
            throw new InvalidOperationException("could not write intermediate trust: passed context incorrect");
        }
    }
}
