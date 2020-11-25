using NeoFS.API.v2.Netmap;

namespace Neo.Fs.Core.Netmap
{
    // Source is an interface that wraps
    // basic network map receiving method.
    public interface ISource
    {
		// GetNetMap reads the diff-th past network map from the storage.
		// Calling with zero diff returns latest network map.
		// It returns the pointer to requested network map and any error encountered.
		//
		// GetNetMap must return exactly one non-nil value.
		// GetNetMap must return ErrNotFound if the network map is not in storage.
		//
		// Implementations must not retain the network map pointer and modify
		// the network map through it.

		NetMap GetNetMap(ulong diff);
	}

	public static class Helper
    {
		public static NetMap GetLatestNetworkMap(this ISource src)
        {
			return src.GetNetMap(0);
        }

		public static NetMap GetPreviousNetworkMap(this ISource src)
		{
			return src.GetNetMap(1);
		}
	}
}
