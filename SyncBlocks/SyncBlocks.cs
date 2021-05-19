using System;
using Akka.Actor;

namespace Cron.Plugins.SyncBlocks
{
    public class SyncBlocks : Plugin
    {
        private IActorRef _bulkImporter;
        
        public override void Configure()
        {
            Settings.Load(GetConfiguration());
            
            // create Import and Export directories
            var importDirectory = Tools.GetImportDirectory();
            var importCreated = Tools.TryCreateDirectory(importDirectory);
            if (!importCreated)
            {
                Console.WriteLine($"Can not create directory {importDirectory}");
            }

            var exportDirectory = Tools.GetExportDirectory();
            var exportCreated = Tools.TryCreateDirectory(exportDirectory);
            if (!exportCreated)
            {
                Console.WriteLine($"Can not create directory {exportDirectory}");
            }
        }

        protected override bool OnMessage(object message)
        {
            if (!(message is string[] args))
                return false;
            if (args.Length == 0)
                return false;
            
            switch (args[0].ToLower())
            {
                case "help":
                    return OnHelp(args);
                case "bulkexport":
                    return OnExport(args);
                case "bulkimport":
                    return OnImport(args);
            }
            return false;
        }
        
        private bool OnHelp(string[] args)
        {
            if (args.Length < 2)
                return false;
            if (!string.Equals(args[1], Name, StringComparison.OrdinalIgnoreCase))
                return false;
            
            Console.Write($"{Name} Commands:\n" + "\tbulkexport\n");
            Console.Write("\tbulkimport\n");
            return true;
        }

        private bool OnExport(string[] args)
        {
            ExportService.ExportData();
            Console.WriteLine();
            return true;
        }
        
        private bool OnImport(string[] args)
        {
            try
            {
                SuspendNodeStartup();
                _bulkImporter = System.ActorSystem.ActorOf(ImportService.Props());

                var importCmd = new PrepareBulkImport(System.Blockchain, OnBulkImportComplete);
                _bulkImporter.Tell(importCmd);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error on import blocks: "+e);
                return false;
            }
        }
        
        private void OnBulkImportComplete()
        {
            ResumeNodeStartup();
            System.ActorSystem.Stop(_bulkImporter);
        }
    }
}