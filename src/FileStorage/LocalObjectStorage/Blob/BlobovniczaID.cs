namespace Neo.FileStorage.LocalObjectStorage.Blob
{
    public class BlobovniczaID
    {
        private readonly byte[] value;

        public bool IsEmpty => value is null || value.Length == 0;

        public BlobovniczaID(byte[] bytes)
        {
            value = bytes;
        }

        public override string ToString()
        {
            return Utility.StrictUTF8.GetString(value);
        }

        public static implicit operator BlobovniczaID(byte[] val)
        {
            return new BlobovniczaID(val);
        }

        public static implicit operator BlobovniczaID(string str)
        {
            return new BlobovniczaID(Utility.StrictUTF8.GetBytes(str));
        }

        public static implicit operator byte[](BlobovniczaID b)
        {
            return b.value;
        }

        public static implicit operator string(BlobovniczaID b)
        {
            return b.ToString();
        }
    }
}
