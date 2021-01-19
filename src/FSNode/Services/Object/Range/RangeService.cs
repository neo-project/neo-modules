using Google.Protobuf;
using V2Range = NeoFS.API.v2.Object.Range;
using NeoFS.API.v2.Refs;
using V2Address = NeoFS.API.v2.Refs.Address;
using Neo.FSNode.Core.Netmap;
using Neo.FSNode.Core.Container;
using Neo.FSNode.Network;
using Neo.FSNode.Services.Object.Head;
using Neo.FSNode.Services.Object.Range.RangeSource;
using Neo.FSNode.Services.Object.Util;
using Neo.FSNode.Services.ObjectManager.Placement;
using System;
using System.Linq;

namespace Neo.FSNode.Services.Object.Range
{
    public class RangeService
    {
        private readonly HeadService headService;
        private Traverser placementTraverser;
        private ILocalAddressSource localAddressSource;
        private INetmapSource netmapSource;
        private IContainerSource containerSource;

        public RangeResult Range(RangePrm prm)
        {
            var head_prm = new HeadPrm
            {
                Address = prm.Address,
            };
            head_prm.WithCommonPrm(prm);
            var head_result = headService.Head(head_prm);
            var origin = head_result.Header;
            if (origin is null) throw new InvalidOperationException(nameof(Range) + " could not receive head result");
            if (prm.Full)
            {
                prm.Range = new V2Range
                {
                    Offset = 0,
                    Length = origin.Header.PayloadLength
                };
            }
            if (origin.Header.PayloadLength < prm.Range.Offset + prm.Range.Length)
                throw new InvalidOperationException(nameof(Range) + " request payload out of range");
            var right = head_result.RightChild;
            if (right is null) right = origin;
            var traverser = new RangeTraverser(origin.Header.PayloadLength, right, prm.Range);
            FillRangeTravers(prm, traverser);
            var data = GetData(prm, traverser);
            return new RangeResult
            {
                Header = origin,
                Chunk = ByteString.CopyFrom(data),
            };
        }

        private void FillRangeTravers(RangePrm prm, RangeTraverser traverser)
        {
            var address = new V2Address
            {
                ContainerId = prm.Address.ContainerId,
            };
            var pair = traverser.Next();
            while (pair.Item2 != null)
            {
                address.ObjectId = pair.Item1;
                var head_prm = new HeadPrm
                {
                    Address = address,
                };
                head_prm.WithCommonPrm(prm);
                var head_result = headService.Head(head_prm);
                if (head_result.Header is null)
                    throw new InvalidOperationException(nameof(Range) + " could not receive head result");
                traverser.PushHeader(head_result.Header);
                pair = traverser.Next();
            }
        }

        private byte[] GetData(RangePrm prm, RangeTraverser range_traverser)
        {
            byte[] chunk = Array.Empty<byte>();
            var address = new V2Address
            {
                ContainerId = prm.Address.ContainerId,
            };
            var pair = range_traverser.Next();
            while (pair.Item2 != null && pair.Item2.Length != 0)
            {
                SwitchToObject(prm, pair.Item1);
                address.ObjectId = pair.Item1;
                var next_range = pair.Item2;
                while (true)
                {
                    var addrs = placementTraverser.Next();
                    if (addrs.Length == 0) break;
                    foreach (var addr in addrs)
                    {
                        IRangeSource rangeSource;
                        if (addr.IsLocalAddress(localAddressSource))
                        {
                            rangeSource = new LocalRangeSource();
                        }
                        else
                        {
                            rangeSource = new RemoteRangeSource(addr);
                        }
                        var piece = rangeSource.Range(address, next_range);
                        range_traverser.PushSuccessSize((ulong)chunk.Length);
                        next_range.Length = next_range.Length - (ulong)chunk.Length;
                        next_range.Offset = next_range.Offset + (ulong)chunk.Length;
                        chunk = chunk.Concat(piece).ToArray();
                        if (next_range.Length == 0)
                        {
                            placementTraverser.SubmitSuccess();
                            break;
                        }
                    }
                    if (!placementTraverser.Success())
                        break;
                }
            }
            return chunk;
        }

        private void SwitchToObject(RangePrm prm, ObjectID oid)
        {
            var nm = netmapSource.GetLatestNetworkMap();
            var container = containerSource.Get(prm.Address.ContainerId);
            var builder = new NetmapBuilder(new NetmapSource(nm));
            if (prm.Local)
                builder = new LocalPlacementBuilder(new NetmapSource(nm), localAddressSource);
            placementTraverser = new Traverser
            {
                Builder = builder,
                Policy = container.PlacementPolicy,
                Address = prm.Address,
                FlatSuccess = 1,
            };
        }
    }
}
