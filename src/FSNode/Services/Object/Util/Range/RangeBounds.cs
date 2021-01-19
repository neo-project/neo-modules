
namespace Neo.FSNode.Services.Object.Util
{
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
}
