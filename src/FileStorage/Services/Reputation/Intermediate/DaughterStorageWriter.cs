using Neo.FileStorage.Services.Reputaion.Common;
using Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Daughters;

namespace Neo.FileStorage.Services.Reputaion.Intermediate
{
    public class DaughterStorageWriter : IWriter
    {
        public ICommonContext Context { get; init; }
        public DaughtersStorage Storage { get; init; }

        public void Write(Trust trust)
        {
            Storage.Put(Context.Epoch, trust);
        }

        public void Close() { }
    }
}
