using System.IO;

namespace Cron.Plugins.SyncBlocks.Model
{
    public class BlockFile
    {
        public string FilePath { get; set; }
        
        public int Index { get; set; }
        
        public bool Processed { get; set; }

        public static BlockFile Create(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return new BlockFile
            {
                FilePath = filePath,
                Index = Tools.GetBlockChunkIndex(fileName),
                Processed = false
            };
        }
    }
}