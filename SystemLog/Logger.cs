using Neo.Plugins;
using System;

namespace SystemLog
{
    public class Logger : Plugin, ILogPlugin
    {
        public override string Name => "SystemLogs";

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public void Log(string source, LogLevel level, string message)
        {
            if (!Settings.Default.ConsoleOutput) return;

            var currentColor = new ConsoleColorSet();

            switch (level)
            {
                case LogLevel.Debug: ConsoleColorSet.Debug.Apply(); break;
                case LogLevel.Error: ConsoleColorSet.Error.Apply(); break;
                case LogLevel.Fatal: ConsoleColorSet.Fatal.Apply(); break;
                case LogLevel.Info: ConsoleColorSet.Info.Apply(); break;
                case LogLevel.Warning: ConsoleColorSet.Warning.Apply(); break;
            }

            Console.WriteLine($"[{DateTime.Now.TimeOfDay:hh\\:mm\\:ss\\.fff}] {message}");

            currentColor.Apply();
        }
    }
}