using System.IO;

namespace Cron.Plugins.SyncBlocks
{
    public static class Tools
    {
        public static bool TryCreateDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            try
            {
                if (Directory.Exists(path))
                    return true;
                
                Directory.CreateDirectory(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public static string GetImportDirectory()
        {
            var assemblyPath = GetAssemblyDirectory();
            var importPart = Settings.Default.ImportDirectory;
            if(importPart.StartsWith("/") || importPart.StartsWith("\\"))
            {
                importPart = importPart.Substring(1);
            }

            if (!string.IsNullOrEmpty(importPart) && !importPart.EndsWith("/") && !importPart.EndsWith("\\"))
            {
                importPart += "/";
            }
            var importPath = assemblyPath.EndsWith("\\") || assemblyPath.EndsWith("/")
                ? $"{assemblyPath}{importPart}"
                : $"{assemblyPath}/{importPart}";
            return importPath;
        }

        public static string GetExportDirectory()
        {
            var assemblyPath = GetAssemblyDirectory();
            var exportPart = Settings.Default.ExportDirectory;
            if(exportPart.StartsWith("/") || exportPart.StartsWith("\\"))
            {
                exportPart = exportPart.Substring(1);
            }

            if (!string.IsNullOrEmpty(exportPart) && !exportPart.EndsWith("/") && !exportPart.EndsWith("\\"))
            {
                exportPart += "/";
            }
            var exportPath = assemblyPath.EndsWith("\\") || assemblyPath.EndsWith("/")
                ? $"{assemblyPath}{exportPart}"
                : $"{assemblyPath}/{exportPart}";
            return exportPath;
        }
        
        private static string GetAssemblyDirectory()
        {
            return Directory.GetCurrentDirectory();
        }

        public static int GetBlockChunkIndex(string fileName)
        {
            if (!fileName.StartsWith("block_chunk_"))
                return 0;
            var n = fileName
                .Replace("block_chunk_", "")
                .Replace(".acc", "");

            if (int.TryParse(n, out var result))
                return result;

            return 0;
        }
    }
}