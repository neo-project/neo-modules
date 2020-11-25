using Neo.Cryptography;

namespace Neo.Fs.LocalObjectStorage.Bucket
{
    public static class Helper
    {
        public const string Name = "filesystem";

        public const string DefaultDirectory = "fsbucket";
        public const int DefaultPermissions = 0755;
        public const int DefaultDepth = 2;
        public const int DefaultPrefixLen = 2;

        public static string StringifyKey(this byte[] key)
        {
            return Base58.Encode(key);
        }

        public static byte[] DecodeKey(this string key)
        {
            return Base58.Decode(key);
        }
    }
}
