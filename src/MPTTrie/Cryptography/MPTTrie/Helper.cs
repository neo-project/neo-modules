using System;

namespace Neo.Cryptography.MPTTrie
{
    public static class Helper
    {
        public static int CompareTo(this byte[] arr1, byte[] arr2)
        {
            if (arr1 is null || arr2 is null) throw new ArgumentNullException();
            for (int i = 0; i < arr1.Length && i < arr2.Length; i++)
            {
                var r = arr1[i].CompareTo(arr2[i]);
                if (r != 0) return r;
            }
            return arr2.Length < arr1.Length ? 1 : arr2.Length == arr1.Length ? 0 : -1;
        }
    }
}
