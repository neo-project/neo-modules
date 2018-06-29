using Neo.Consensus;
using System;
using System.IO;

namespace Neo.Plugins
{
    public class ConsensusLogger : LogPlugin
    {
        private static string log_dictionary = Path.Combine(AppContext.BaseDirectory, "Logs");

        public override string Name => nameof(ConsensusLogger);

        protected override void OnLog(string source, LogLevel level, string message)
        {
            if (source != nameof(ConsensusService)) return;
            DateTime now = DateTime.Now;
            string line = $"[{now.TimeOfDay:hh\\:mm\\:ss}] {message}";
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
