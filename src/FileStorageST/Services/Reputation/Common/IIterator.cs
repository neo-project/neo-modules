using Neo.FileStorage.API.Reputation;
using System;

namespace Neo.FileStorage.Storage.Services.Reputaion.Common
{
    public interface IIterator
    {
        void Iterate(Action<PeerToPeerTrust> handler);
    }
}
