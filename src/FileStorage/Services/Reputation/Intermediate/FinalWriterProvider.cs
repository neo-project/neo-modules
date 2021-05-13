using System.Security.Cryptography;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Reputaion.EigenTrust;

namespace Neo.FileStorage.Services.Reputaion.Intermediate
{
    public class FinalWriterProvider
    {
        public ECDsa PrivateKey { get; init; }
        public byte[] PublicKey { get; init; }
        public Client MorphClient { get; init; }

        public FinalWriter InitIntermediateWriter()
        {
            return new()
            {
                PrivateKey = PrivateKey,
                PublicKey = PublicKey,
                MorphClient = MorphClient,
            };
        }
    }
}
