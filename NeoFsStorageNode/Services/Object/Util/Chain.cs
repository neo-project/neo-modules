using NeoFS.API.v2.Refs;
using V2Object = NeoFS.API.v2.Object.Object;
using System;
using System.Collections.Generic;
using System.Text;
using V2Range = NeoFS.API.v2.Object.Range;

namespace Neo.Fs.Services.Object.Util
{
    public class RangeChain
    {
        private RangeChain prev, next;
        private RangeBounds bounds;
        private ObjectID id;

        public RangeChain Prev
        {
            get => prev;
            set => this.prev = value;
        }

        public RangeChain Next
        {
            get => next;
            set => this.next = value;
        }


        public RangeChain(RangeBounds rngBounds, ObjectID objId, RangeChain previous = null, RangeChain nxt = null)
        {
            this.bounds = rngBounds;
            this.id = objId;
            this.prev = previous;
            this.next = nxt;
        }
    }

    public class RangeBounds
    {
        private ulong left;
        private ulong right;

        public RangeBounds(ulong l, ulong r)
        {
            this.left = l;
            this.right = r;
        }
    }

    public class RangeTraverser
    {
        private RangeChain chain;
        private RangeBounds seekBounds;

        public RangeTraverser(ulong originSize, V2Object rightElement, V2Range rngSeek)
        {
            var right = new RangeChain(
                new RangeBounds(originSize - (ulong)rightElement.CalculateSize(), originSize),
                rightElement.ObjectId);

            var left = new RangeChain(null, rightElement.Header.Split.Previous);

            left.Next = right;
            right.Prev = left;

            this.chain = right;
            this.seekBounds = new RangeBounds(rngSeek.Offset, rngSeek.Offset + rngSeek.Length);
        }
    }


}
