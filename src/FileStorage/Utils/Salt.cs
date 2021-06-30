
using System.Linq;

namespace Neo.FileStorage.Utils
{
    public static partial class Utils
    {
        public static byte[] SaltXOR(this byte[] data, byte[] salt)
        {
            if (salt is null || !salt.Any())
                return data;
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                result[i] = (byte)(data[i] ^ salt[i % salt.Length]);
            return result;
        }
    }
}