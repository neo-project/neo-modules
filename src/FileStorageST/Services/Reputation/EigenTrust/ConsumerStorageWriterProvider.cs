using Neo.FileStorage.Storage.Services.Reputaion.Common;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Consumers;
using System;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust
{
    public class ConsumerStorageWriterProvider : IWriterProvider
    {
        public ConsumersStorage Storage { get; init; }

        public IWriter InitWriter(ICommonContext context)
        {
            if (context is IterationContext ictx)
            {
                return new ConsumerStorageWriter
                {
                    Context = ictx,
                    Storage = Storage,
                };
            }
            throw new InvalidOperationException($"could not write intermediate trust: passed context incorrect, type={context.GetType()}");
        }
    }
}
