using NeoFS.API.v2.Refs;
using System;
using V2Object = NeoFS.API.v2.Object.Object;
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

        public ObjectID Id
        {
            get => this.id;
            set => this.id = value;
        }

        public RangeBounds Bounds
        {
            get => this.bounds;
            set => this.bounds = value;
        }

        //public RangeChain(RangeBounds rngBounds, ObjectID objId, RangeChain previous = null, RangeChain nxt = null)
        //{
        //    this.bounds = rngBounds;
        //    this.id = objId;
        //    this.prev = previous;
        //    this.next = nxt;
        //}
    }

    public class RangeBounds
    {
        private ulong left;
        private ulong right;

        public ulong Left
        {
            get => this.left;
            set => this.left = value;
        }

        public ulong Right
        {
            get => this.right;
            set => this.right = value;
        }

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
            var right = new RangeChain()
            {
                Bounds = new RangeBounds(originSize - (ulong)rightElement.CalculateSize(), originSize),
                Id = rightElement.ObjectId
            };

            var left = new RangeChain()
            {
                Id = rightElement.Header.Split.Previous
            };

            left.Next = right;
            right.Prev = left;

            this.chain = right;
            this.seekBounds = new RangeBounds(rngSeek.Offset, rngSeek.Offset + rngSeek.Length);
        }

        public (ObjectID, V2Range) Next()
        {
            var left = this.chain.Bounds.Left;
            var seekLeft = this.seekBounds.Left;
            ObjectID id;
            V2Range range = null;

            if (left > seekLeft)
                id = this.chain.Prev.Id;
            else
            {
                id = this.chain.Id;
                range = new V2Range() { Offset = seekLeft - left, Length = Math.Min(this.chain.Bounds.Right, this.seekBounds.Right) - seekLeft };
            }
            return (id, range);
        }

        public void PushHeader(V2Object obj)
        {
            var id = obj.ObjectId;
            if (!id.Equals(this.chain.Prev.Id))
                throw new ArgumentException("unexpected identifier in header");

            var sz = obj.Header.PayloadLength;
            this.chain.Prev.Bounds = new RangeBounds(this.chain.Bounds.Left - sz, this.chain.Bounds.Left);
            this.chain = this.chain.Prev;
            var prev = obj.Header.Split.Previous;
            if (prev != null)
                this.chain.Prev = new RangeChain() { Next = this.chain, Id = prev };
        }

        public void PushSuccessSize(ulong sz)
        {
            this.seekBounds.Left += sz;
            if (this.seekBounds.Left >= this.chain.Bounds.Right && this.chain.Next != null)
                this.chain = this.chain.Next;
        }

        public void SetSeekRange(V2Range rng)
        {
            var offset = rng.Offset;
            while (true)
            {
                if (offset < this.chain.Bounds.Left)
                {
                    if (this.chain.Prev == null)
                        break;
                    this.chain = this.chain.Prev;
                }
                else if (offset >= this.chain.Bounds.Left && offset < this.chain.Bounds.Right)
                    break;
                else if (offset >= this.chain.Bounds.Right)
                {
                    if (this.chain.Next == null)
                        break;
                    this.chain = this.chain.Next;
                }
            }

            this.seekBounds = new RangeBounds(offset, offset + rng.Length);
        }
    }
}
