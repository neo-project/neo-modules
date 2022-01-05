using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Storage.Services.Reputaion.Common;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Daughters;
using System;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust
{
    public class DaughterStorageWriter : IWriter
    {
        public ICommonContext Context { get; init; }
        public DaughtersStorage Storage { get; init; }

        public void Write(PeerToPeerTrust trust)
        {
            Storage.Put(Context.Epoch, trust);
        }

        public void Close() { }
    }
}
