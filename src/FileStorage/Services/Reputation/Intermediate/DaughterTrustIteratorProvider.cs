using Neo.FileStorage.Services.Reputaion.EigenTrust;
using Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Consumers;
using Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Daughters;
using System;

namespace Neo.FileStorage.Services.Reputaion.Intermediate
{
    public class DaughterTrustIteratorProvider
    {
        public EigenTrust.Storage.Consumers.Storage ConsumerStorage { get; init; }
        public EigenTrust.Storage.Daughters.Storage DaughterStorage { get; init; }

        public DaughterTrusts InitDaughterIterator(IterationContext context, PeerID peer)
        {
            if (DaughterStorage.DaughterTrusts(context.Epoch, peer, out DaughterTrusts storage))
                return storage;
            throw new InvalidOperationException();
        }

        public DaughterStorage InitAllDaughtersIterator(IterationContext context)
        {
            if (DaughterStorage.AllDaughterTrusts(context.Epoch, out DaughterStorage storage))
                return storage;
            throw new InvalidOperationException();
        }

        public ConsumersStorage InitConsumerIterator(IterationContext context)
        {
            if (ConsumerStorage.Consumers(context.Epoch, context.Index, out ConsumersStorage storage))
                return storage;
            throw new InvalidOperationException();
        }
    }
}
