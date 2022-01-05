using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Storage.Services.Reputaion.Common;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Consumers;
using System;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust
{
    public class ConsumerStorageWriter : IWriter
    {
        public IterationContext Context { get; init; }
        public ConsumersStorage Storage { get; init; }

        public void Write(PeerToPeerTrust trust)
        {
            IterationTrust t = new()
            {
                Epoch = Context.Epoch,
                Index = Context.Index,
                Trust = trust,
            };
            Storage.Put(t);
        }

        public void Close() { }
    }
}
