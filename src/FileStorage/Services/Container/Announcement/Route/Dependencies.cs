using Google.Protobuf;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Services.Container.Announcement.Control;
using System.Collections.Generic;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement.Route
{
    public interface IBuilder
    {
        List<NodeInfo> NextStage(FSAnnouncement announcement, List<NodeInfo> passed);
    }

    public interface IRemoteWriterProvider
    {
        IWriterProvider InitRemote(NodeInfo info);
    }
}
