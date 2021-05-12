using System;
using System.Security.Cryptography;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Reputaion.EigenTrust;

namespace Neo.FileStorage.Services.Reputaion.Intermediate
{
    public class FinalWriter
    {
        public ECDsa PrivateKey { get; init; }
        public byte[] PublicKey { get; init; }
        public MorphClient MorphClient { get; init; }

        public void WriteIntermediateTrust(IterationTrust it)
        {
            throw new NotImplementedException();
        }
    }
}
