using System.Collections.Generic;
using System.Linq;

namespace Cron.Plugins.SyncBlocks.Extensions
{
    public static class EnumerableExtensions
    {
        public static List<List<T>> ChunkBy<T>(this IEnumerable<T> source, int chunkSize) 
        {
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / chunkSize)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }

        public static List<T> Extract<T>(this List<T> source, int size)
        {
            var items = source.Take(size).ToList();
			
            var removeCount = source.Count > size
                ? size
                : source.Count;
            source.RemoveRange(0, removeCount);
            return items;
        }
    }
}