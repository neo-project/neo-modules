using Neo.FileStorage.Services.Reputaion.Common;
using Neo.FileStorage.Services.Reputaion.EigenTrust;
using Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Consumers;
using Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Daughters;

namespace Neo.FileStorage.Services.Reputaion.Intermediate
{
    public class ConsumerStorageWriter : IWriter
    {
        public IterationContext Context { get; init; }
        public ConsumersStorage Storage { get; init; }

        public void Write(Trust trust)
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
