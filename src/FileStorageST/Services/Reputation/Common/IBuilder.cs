using System.Collections.Generic;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Reputation;

namespace Neo.FileStorage.Storage.Services.Reputaion.Common
{
    public interface IBuilder
    {
        List<NodeInfo> NextStage(ulong epoch, PeerToPeerTrust trust, List<NodeInfo> passed);
    }
}
