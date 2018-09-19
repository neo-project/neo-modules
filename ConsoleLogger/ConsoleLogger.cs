using System;

namespace Neo.Plugins
{
    /// <summary>
    /// Console show log
    /// </summary>
    public class ConsoleLogger : Plugin, ILogPlugin
    {
        public new void Log(string source, LogLevel level, string message)
        {
            DateTime now = DateTime.Now;
            string line = $"[{now.TimeOfDay:hh\\:mm\\:ss\\:fff}] [{source}][{level}]{message}";
            Console.WriteLine(line);
        }
    }
}
