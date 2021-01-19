namespace Neo.FSNode.LocalObjectStorage.Bucket
{
    public delegate bool FilterHandler(byte[] key, byte[] val);
    public interface IBucket
    {
        byte[] Get(byte[] key);
        void Set(byte[] key, byte[] value);
        void Del(byte[] key);
        bool Has(byte[] key);
        long Size();
        byte[][] List();
        void Iterate(FilterHandler filterHandler);
        void Close();
    }

    public class BucketItem
    {
        public byte[] Key { get; set; }
        public byte[] Val { get; set; }
    }
}
