using Grpc.Core;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Services.Container.Announcement.Route;
using Neo.FileStorage.Services.Container.Announcement.Route.Placement;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement
{
    public class UsedSpaceService
    {
        private readonly ECDsa key;
        private readonly NodeInfo localNodeInfo;
        private readonly Router router;
        private readonly LoadPlacementBuilder loadbuilder;
        private readonly RouteBuilder routeBuilder;

        public AnnounceUsedSpaceResponse AnnounceUsedSpace(AnnounceUsedSpaceRequest request)
        {
            List<NodeInfo> passed = new();
            for (var header = request.VerifyHeader; header != null; header = header.Origin)
            {
                passed.Add(new() { PublicKey = header.BodySignature.Key });
            }
            passed.Reverse();
            passed.Add(localNodeInfo);

            var writer = router.InitWriter(new RouteContext(passed, new CancellationTokenSource().Token));//TODO: fix cancellation token
            foreach (var announcement in request.Body.Announcements)
            {
                ProcessLoadValue(announcement, passed, writer);
            }
            var resp = new AnnounceUsedSpaceResponse()
            {
                Body = new AnnounceUsedSpaceResponse.Types.Body { }
            };
            key.SignResponse(resp);
            return resp;
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
