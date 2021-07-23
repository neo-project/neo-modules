using System.Security.Cryptography;
using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust;

namespace Neo.FileStorage.Storage.Services.Reputaion.Intermediate
{
    public class FinalWriter
    {
        public ECDsa PrivateKey { get; init; }
        public byte[] PublicKey { get; init; }
        public MorphInvoker MorphInvoker { get; init; }

        public void WriteIntermediateTrust(IterationTrust it)
        {
            Trust trust = it.Trust.Trust;
            GlobalTrust gt = new()
            {
                Body = new()
                {
                    Manager = new()
                    {
                        PublicKey = ByteString.CopyFrom(PublicKey),
                    },
                    Trust = trust,
                }
            };
            gt.Signature = PrivateKey.SignMessagePart(gt.Body);
            MorphInvoker.PutReputation(it.Epoch, trust.Peer.ToByteArray(), gt.ToByteArray());
        }
    }
}
