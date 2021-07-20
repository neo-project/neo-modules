using System;
using System.Collections.Generic;
using System.Threading;
using Google.Protobuf;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Storage.Services.Reputaion.Common;
using Neo.FileStorage.Storage.Services.Reputaion.Common.Route;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust;

namespace Neo.FileStorage.Storage.Services.Reputaion.Service
{
    public class ReputationService
    {
        public NodeInfo LocalNodeInfo { get; init; }
        public IWriterProvider LocalRouter { get; init; }
        public IWriterProvider IntermediateRouter { get; init; }
        public IBuilder RouteBuilder { get; init; }

        public AnnounceIntermediateResultResponse AnnounceIntermediateResult(AnnounceIntermediateResultRequest request, CancellationToken cancellation)
        {
            var vheader = request.VerifyHeader;
            var passed = ReverseRoute(ref vheader);
            passed.Add(LocalNodeInfo);
            var body = request.Body;
            var ctx = new IterationContext
            {
                Cancellation = cancellation,
                Epoch = body.Epoch,
                Index = body.Iteration,
            };
            var writer = IntermediateRouter.InitWriter(ctx);
            writer.Write(body.Trust);
            var resp = new AnnounceIntermediateResultResponse
            {
                Body = new()
            };
            return resp;
        }
        public AnnounceLocalTrustResponse AnnounceLocalTrust(AnnounceLocalTrustRequest request, CancellationToken cancellation)
        {
            var vheader = request.VerifyHeader;
            var passed = ReverseRoute(ref vheader);
            passed.Add(LocalNodeInfo);
            var body = request.Body;
            var ctx = new EpochContext
            {
                Cancellation = cancellation,
                Epoch = body.Epoch
            };
            var writer = LocalRouter.InitWriter(ctx);
            foreach (var trust in body.Trusts)
            {
                PeerToPeerTrust t = new()
                {
                    TrustingPeer = new() { PublicKey = passed[0].PublicKey },
                    Trust = trust,
                };
                if (!RouteBuilder.CheckRoute(body.Epoch, t, passed))
                    throw new InvalidOperationException("could not write one of local trusts");
                writer.Write(t);
            }
            var resp = new AnnounceLocalTrustResponse
            {
                Body = new()
            };
            return resp;
        }

        private List<NodeInfo> ReverseRoute(ref RequestVerificationHeader header)
        {
            List<NodeInfo> result = new();
            if (header is not null)
            {
                result.Add(new NodeInfo
                {
                    PublicKey = header.BodySignature.Key,
                });
                header = header.Origin;
            }
            return result;
        }
    }
}
