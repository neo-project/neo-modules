using Grpc.Core;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Services.Container.Announcement.Route;
using Neo.FileStorage.Services.Container.Announcement.Route.Placement;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement
{
    public class UsedSpaceService
    {
        private readonly NodeInfo localNodeInfo;
        private readonly Router router;
        private readonly LoadPlacementBuilder loadbuilder;
        private readonly RouteBuilder routeBuilder;

        public Task<AnnounceUsedSpaceResponse> AnnounceUsedSpace(AnnounceUsedSpaceRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                List<NodeInfo> passed = new();
                for (var header = request.VerifyHeader; header != null; header = header.Origin)
                {
                    passed.Add(new() { PublicKey = header.BodySignature.Key });
                }
                passed.Reverse();
                passed.Add(localNodeInfo);
                var writer = router.InitWriter(new RouteContext(passed, context.CancellationToken));
                foreach (var announcement in request.Body.Announcements)
                {
                    ProcessLoadValue(announcement, passed, writer);
                }
                var resp = new AnnounceUsedSpaceResponse()
                {
                    Body = new AnnounceUsedSpaceResponse.Types.Body { }
                };
                return resp;
            });
        }

        private void ProcessLoadValue(FSAnnouncement announcement, List<NodeInfo> route, LoadWriter writer)
        {
            if (!loadbuilder.IsNodeFromContainerKey(announcement.Epoch, announcement.ContainerId, route[0].PublicKey.ToByteArray()))
                throw new Exception("node outside the container");
            routeBuilder.CheckRoute(announcement, route);
            writer.Put(announcement);
        }
    }
}
