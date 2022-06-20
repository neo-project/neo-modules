using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Consumers;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Daughters;
using System;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust
{
    public class DaughterTrustIteratorProvider
    {
        public ConsumersStorage ConsumerStorage { get; init; }
        public DaughtersStorage DaughterStorage { get; init; }

        public DaughterTrusts InitDaughterIterator(IterationContext context, PeerID peer)
        {
            if (DaughterStorage.DaughterTrusts(context.Epoch, peer, out DaughterTrusts storage))
                return storage;
            throw new InvalidOperationException($"daughter trust iterator init failure, epoch={context.Epoch}, peer={peer.PublicKey.ToBase64()}");
        }

        public DaughterStorage InitAllDaughtersIterator(IterationContext context)
        {
            if (DaughterStorage.AllDaughterTrusts(context.Epoch, out DaughterStorage storage))
                return storage;
            throw new InvalidOperationException($"all daughters trust iterator init failure, epoch={context.Epoch}");
        }

        public ConsumerStorage InitConsumerIterator(IterationContext context)
        {
            if (ConsumerStorage.Consumers(context.Epoch, context.Index, out ConsumerStorage storage))
                return storage;
            throw new InvalidOperationException($"consumer trust iterator init failure, epoch={context.Epoch}");
        }
    }
}
