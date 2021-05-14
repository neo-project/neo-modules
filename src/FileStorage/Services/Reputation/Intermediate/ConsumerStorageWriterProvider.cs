using System;
using Neo.FileStorage.Services.Reputaion.Common;
using Neo.FileStorage.Services.Reputaion.EigenTrust;
using Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Consumers;

namespace Neo.FileStorage.Services.Reputaion.Intermediate
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
            throw new InvalidOperationException("could not write intermediate trust: passed context incorrect");
        }
    }
}
