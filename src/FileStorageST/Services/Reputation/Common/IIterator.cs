using System;
using Neo.FileStorage.API.Reputation;

namespace Neo.FileStorage.Storage.Services.Reputaion.Common
{
    public interface IIterator
    {
        void Iterate(Action<PeerToPeerTrust> handler);
    }
}
