using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Invoker.Morph;
using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust
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
            Utility.Log(nameof(FinalWriter), LogLevel.Debug, $"put reputation, peer={trust.Peer.PublicKey.ToBase64()}, global_trust={trust.Value}");
            MorphInvoker.PutReputation(it.Epoch, trust.Peer.ToByteArray(), gt.ToByteArray());
        }
    }
}
