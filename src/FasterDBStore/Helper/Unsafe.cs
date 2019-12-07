using System.Runtime.CompilerServices;

namespace Neo.Plugins.Storage.Helper
{
    internal unsafe class Unsafe
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool MemoryEquals(byte[] x, byte[] y)
        {
            if (x == y) return true;
            int len = x.Length;
            if (len != y.Length) return false;
            if (len == 0) return true;

            fixed (byte* xp = x, yp = y)
            {
                long* xlp = (long*)xp, ylp = (long*)yp;
                for (; len >= 8; len -= 8)
                {
                    if (*xlp != *ylp) return false;
                    xlp++;
                    ylp++;
                }
                byte* xbp = (byte*)xlp, ybp = (byte*)ylp;
                for (; len > 0; len--)
                {
                    if (*xbp != *ybp) return false;
                    xbp++;
                    ybp++;
                }
            }

            return true;
        }

        internal static long GetHashCode(byte[] x)
        {
            int len = x.Length;
            if (len == 0) return 0;

            long ret = 127;

            fixed (byte* xp = x)
            {
                long* xlp = (long*)xp;
                for (; len >= 8; len -= 8)
                {
                    if (*xlp != 0) ret *= *xlp;
                    xlp++;
                }

                byte* xbp = (byte*)xlp;
                for (; len > 0; len--)
                {
                    if (*xbp != 0) ret *= *xbp;
                    xbp++;
                }
            }

            return ret;
        }
    }
}
