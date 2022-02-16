using Neo.FileStorage.Invoker.Morph;
using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust
{
    public class FinalWriterProvider
    {
        public ECDsa PrivateKey { get; init; }
        public byte[] PublicKey { get; init; }
        public MorphInvoker MorphInvoker { get; init; }

        public FinalWriter InitIntermediateWriter()
        {
            return new()
            {
                PrivateKey = PrivateKey,
                PublicKey = PublicKey,
                MorphInvoker = MorphInvoker,
            };
        }
    }
}