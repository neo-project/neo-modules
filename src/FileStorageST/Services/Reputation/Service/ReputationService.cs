using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Storage.Services.Reputaion.Common;
using Neo.FileStorage.Storage.Services.Reputaion.Common.Route;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Reputaion.Service
{
    public class ReputationService
    {
        public ILocalInfoSource LocalInfoSource { get; init; }
        public IWriterProvider LocalRouter { get; init; }
        public IWriterProvider IntermediateRouter { get; init; }
        public IBuilder RouteBuilder { get; init; }

        public AnnounceLocalTrustResponse AnnounceLocalTrust(AnnounceLocalTrustRequest request, CancellationToken cancellation)
        {
            var vheader = request.VerifyHeader;
            var passed = ReverseRoute(vheader);
            passed.Add(LocalInfoSource.NodeInfo);
            var body = request.Body;
            var ctx = new EpochContext
            {
                Cancellation = cancellation,
                Epoch = body.Epoch
            };
            var writer = LocalRouter.InitWriter(new RouteContext(ctx, passed));
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

        public AnnounceIntermediateResultResponse AnnounceIntermediateResult(AnnounceIntermediateResultRequest request, CancellationToken cancellation)
        {
            var vheader = request.VerifyHeader;
            var passed = ReverseRoute(vheader);
            passed.Add(LocalInfoSource.NodeInfo);
            var body = request.Body;
            var ctx = new IterationContext
            {
                Cancellation = cancellation,
                Epoch = body.Epoch,
                Index = body.Iteration,
            };
            var writer = IntermediateRouter.InitWriter(new RouteContext(ctx, passed));
            writer.Write(body.Trust);
            var resp = new AnnounceIntermediateResultResponse
            {
                Body = new()
            };
            return resp;
        }

        private List<NodeInfo> ReverseRoute(RequestVerificationHeader header)
        {
            List<NodeInfo> result = new();
            while (header is not null)
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
