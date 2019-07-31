using Neo.Consensus;
using System;
using System.IO;


namespace Neo.Plugins
{
    public class SimplePolicyPlugin : Plugin, ILogPlugin
    {
        private static readonly string log_dictionary = Path.Combine(AppContext.BaseDirectory, "Logs");

        void ILogPlugin.Log(string source, LogLevel level, string message)
        {
            if (source != nameof(ConsensusService)) return;
            DateTime now = DateTime.Now;
            string line = $"[{now.TimeOfDay:hh\\:mm\\:ss\\.fff}] {message}";
            Console.WriteLine(line);
            if (string.IsNullOrEmpty(log_dictionary)) return;
            lock (log_dictionary)
            {
                Directory.CreateDirectory(log_dictionary);
                string path = Path.Combine(log_dictionary, $"{now:yyyy-MM-dd}.log");
                File.AppendAllLines(path, new[] { line });
            }
        }
    }
}

