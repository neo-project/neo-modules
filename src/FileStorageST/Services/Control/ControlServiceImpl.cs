using Google.Protobuf;
using Grpc.Core;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Control;
using Neo.FileStorage.Reputation;
using Neo.FileStorage.Storage.LocalObjectStorage.Engine;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using APINodeInfo = Neo.FileStorage.API.Netmap.NodeInfo;
using System;

namespace Neo.FileStorage.Storage.Services.Control
{
    public class ControlServiceImpl : ControlService.ControlServiceBase
    {
        public ECDsa Key { get; init; }
        public StorageEngine LocalStorage { get; init; }
        public IEpochSource EpochSource { get; init; }
        public INetmapSource NetmapSource { get; init; }
        public StorageService StorageService { get; init; }
        public readonly HashSet<ByteString> AllowKeys = new();

        public override Task<DropObjectsResponse> DropObjects(DropObjectsRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!IsValidRequest(request)) throw new RpcException(new Status(StatusCode.PermissionDenied, "invalid admin"));
                    var addresses = new Address[request.Body.AddressList.Count];
                    for (int i = 0; i < request.Body.AddressList.Count; i++)
                    {
                        addresses[i] = Address.Parser.ParseFrom(request.Body.AddressList[i]);
                    }
                    LocalStorage.Delete(addresses);
                    var resp = new DropObjectsResponse
                    {
                        Body = new DropObjectsResponse.Types.Body(),
                    };
                    Key.SignControlMessage(resp);
                    return resp;
                }
                catch (Exception e)
                {
                    throw new RpcException(new Status(StatusCode.Unknown, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task<HealthCheckResponse> HealthCheck(HealthCheckRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!IsValidRequest(request)) throw new RpcException(new Status(StatusCode.PermissionDenied, "invalid admin"));
                    var resp = new HealthCheckResponse
                    {
                        Body = new HealthCheckResponse.Types.Body
                        {
                            NetmapStatus = (NetmapStatus)StorageService.NodeInfo.State,
                            HealthStatus = StorageService.HealthStatus,
                        }
                    };
                    Key.SignControlMessage(resp);
                    return resp;
                }
                catch (Exception e)
                {
                    throw new RpcException(new Status(StatusCode.Unknown, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task<NetmapSnapshotResponse> NetmapSnapshot(NetmapSnapshotRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!IsValidRequest(request)) throw new RpcException(new Status(StatusCode.PermissionDenied, "invalid admin"));
                    var epoch = EpochSource.CurrentEpoch;
                    var nm = NetmapSource.GetNetMapByEpoch(epoch);
                    var netmap = new API.Control.Netmap
                    {
                        Epoch = epoch,
                    };
                    netmap.Nodes.AddRange(nm.Nodes.Select(p => NodeInfoFromAPI(p.Info)));
                    var resp = new NetmapSnapshotResponse
                    {
                        Body = new NetmapSnapshotResponse.Types.Body
                        {
                            Netmap = netmap,
                        },
                    };
                    Key.SignControlMessage(resp);
                    return resp;
                }
                catch (Exception e)
                {
                    throw new RpcException(new Status(StatusCode.Unknown, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task<SetNetmapStatusResponse> SetNetmapStatus(SetNetmapStatusRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!IsValidRequest(request)) throw new RpcException(new Status(StatusCode.PermissionDenied, "invalid admin"));
                    StorageService.SetStatus(request.Body.Status);
                    var resp = new SetNetmapStatusResponse
                    {
                        Body = new SetNetmapStatusResponse.Types.Body { }
                    };
                    Key.SignControlMessage(resp);
                    return resp;
                }
                catch (Exception e)
                {
                    throw new RpcException(new Status(StatusCode.Unknown, e.Message));
                }
            }, context.CancellationToken);
        }

        private bool IsValidRequest(IControlMessage message)
        {
            var key = message.Signature.Key;
            if (!AllowKeys.Contains(key))
                return false;
            return message.VerifyControlMessage();
        }

        private NodeInfo NodeInfoFromAPI(APINodeInfo n)
        {
            NodeInfo ni = new()
            {
                PublicKey = n.PublicKey,
                State = (NetmapStatus)n.State,
            };
            ni.Addresses.AddRange(n.Addresses);
            ni.Attributes.AddRange(n.Attributes.Select(p => AttributeFromAPI(p)));
            return ni;
        }

        private NodeInfo.Types.Attribute AttributeFromAPI(APINodeInfo.Types.Attribute a)
        {
            return new()
            {
                Key = a.Key,
                Value = a.Value,
            };
        }
    }
}
