using NeoFS.API.v2.Refs;
using V2Range = NeoFS.API.v2.Object.Range;
using Neo.FSNode.Services.Object.Head;
using Neo.FSNode.Services.Object.Range;
using Neo.FSNode.Services.Object.Util;
using Neo.Cryptography;
using System;
using System.Collections.Generic;

namespace Neo.FSNode.Services.Object.RangeHash
{
    public class RangeHashService
    {
        private readonly HeadService headService;
        private readonly RangeService rangeService;

        public RangeHashResult RangeHash(RangeHashPrm prm)
        {
            var head_prm = new HeadPrm
            {
                Address = prm.Address,
            };
            head_prm.WithCommonPrm(prm);
            var head_result = headService.Head(head_prm);
            if (head_result is null)
                throw new InvalidOperationException(nameof(RangeHash) + " could not receive head result");
            var origin = head_result.Header;
            var origin_size = origin.Header.PayloadLength;
            ulong min_left = 0, max_right = 0;
            foreach (var range in prm.Ranges)
            {
                var left = range.Offset;
                var right = left + range.Length;
                if (origin_size < right)
                    throw new InvalidOperationException(nameof(RangeHash) + " requested range out of bound");
                if (left < min_left)
                    min_left = left;
                if (max_right < right)
                    max_right = right;
            }
            var right_child = head_result.RightChild;
            if (right_child is null)
                right_child = origin;
            var border = new V2Range
            {
                Offset = min_left,
                Length = max_right - min_left,
            };
            var range_traverser = new RangeTraverser(origin_size, right_child, border);
            return GetHashes(prm, range_traverser);
        }

        private RangeHashResult GetHashes(RangeHashPrm prm, RangeTraverser traverser)
        {
            var address = new Address
            {
                ContainerId = prm.Address.ContainerId,
            };
            var hashes = new List<byte[]>();
            foreach (var range in prm.Ranges)
            {
                var next = traverser.Next();
                while (next.Item2 != null)
                {
                    address.ObjectId = next.Item1;
                    var head_prm = new HeadPrm
                    {
                        Address = address,
                    };
                    head_prm.WithCommonPrm(prm);
                    var head_result = headService.Head(head_prm);
                    if (head_result is null)
                        throw new InvalidOperationException(nameof(GetHashes) + $" could not get header {address.String()}");
                    traverser.PushHeader(head_result.Header);
                    next = traverser.Next();
                }
                traverser.SetSeekRange(range);
                IHasher hasher = null;
                next = traverser.Next();
                while (true)
                {
                    next = traverser.Next();
                    if (next.Item2.Length == 0) break;
                    address.ObjectId = next.Item1;
                    if (prm.HashType == ChecksumType.Sha256 && next.Item2.Length != range.Length)
                    {
                        var range_prm = new RangePrm
                        {
                            Address = address,
                            Range = next.Item2,
                        };
                        range_prm.WithCommonPrm(prm);
                        var range_result = rangeService.Range(range_prm);
                        if (range_result is null)
                            throw new InvalidOperationException(nameof(GetHashes) + $" could not receive payload range for checksum");
                        hasher.Add(range_result.Chunk.ToByteArray());
                    }
                    else
                    {
                        var distributedHasher = new DistributedHasher();
                        var dprm = new RangeHashPrm
                        {
                            Address = address,
                            HashType = prm.HashType,
                            Ranges = prm.Ranges,
                        };
                        dprm.WithCommonPrm(prm);
                        var resp = distributedHasher.Head(dprm);
                        if (resp is null)
                            throw new InvalidOperationException(nameof(GetHashes) + " could not receive checksum");
                        if (resp.Hashes.Count != 1)
                            throw new InvalidOperationException(nameof(GetHashes) + " could not calculate checksum");
                        hasher.Add(resp.Hashes[0]);
                    }
                    traverser.PushSuccessSize(next.Item2.Length);
                }
                var hash = hasher.Sum();
                hashes.Add(hash);
            }
            return new RangeHashResult
            {
                Hashes = hashes,
            };
        }
    }
}
