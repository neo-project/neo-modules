using System;
using System.IO;

namespace Neo.Plugins
{
    public class Logger : Plugin, ILogPlugin
    {
        public override string Name => "SystemLogs";

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public new void Log(string source, LogLevel level, string message)
        {
            DateTime now = DateTime.Now;
            var log = $"[{now.TimeOfDay:hh\\:mm\\:ss\\.fff}] {message}";

            if (Settings.Default.ConsoleOutput)
            {
                var currentColor = new ConsoleColorSet();

                switch (level)
                {
                    case LogLevel.Debug: ConsoleColorSet.Debug.Apply(); break;
                    case LogLevel.Error: ConsoleColorSet.Error.Apply(); break;
                    case LogLevel.Fatal: ConsoleColorSet.Fatal.Apply(); break;
                    case LogLevel.Info: ConsoleColorSet.Info.Apply(); break;
                    case LogLevel.Warning: ConsoleColorSet.Warning.Apply(); break;
                }

                Console.WriteLine(log);
                currentColor.Apply();
            }

            if (!string.IsNullOrEmpty(Settings.Default.Path))
            {
                var path = Path.Combine(Settings.Default.Path, $"{now:yyyy-MM-dd}.log");
                File.AppendAllText(path, log + Environment.NewLine);
            }
        }
    }
}