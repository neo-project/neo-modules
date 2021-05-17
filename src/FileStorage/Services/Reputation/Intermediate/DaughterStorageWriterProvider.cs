using Neo.FileStorage.Services.Reputaion.Common;
using Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Daughters;

namespace Neo.FileStorage.Services.Reputaion.Intermediate
{
    public class DaughterStorageWriterProvider : IWriterProvider
    {
        public DaughtersStorage Storage { get; init; }

        public IWriter InitWriter(ICommonContext context)
        {
            return new DaughterStorageWriter
            {
                Context = context,
                Storage = Storage,
            };
        }
    }
}
