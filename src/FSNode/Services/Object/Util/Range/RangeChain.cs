using NeoFS.API.v2.Refs;

namespace Neo.FSNode.Services.Object.Util
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
}
