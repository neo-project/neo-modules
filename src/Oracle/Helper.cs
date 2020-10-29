using System;
using System.Text;

namespace Neo.Plugins
{
    internal static class Helper
    {
        public static string ReadUserInput(string prompt, bool password = false)
        {
            const string t = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            StringBuilder sb = new StringBuilder();
            ConsoleKeyInfo key;

            if (!string.IsNullOrEmpty(prompt))
            {
                Console.Write(prompt + ": ");
            }

            var prevForeground = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;

            if (Console.IsInputRedirected)
            {
                // neo-gui Console require it
                sb.Append(Console.ReadLine());
            }
            else
            {
                do
                {
                    key = Console.ReadKey(true);

                    if (t.IndexOf(key.KeyChar) != -1)
                    {
                        sb.Append(key.KeyChar);
                        if (password)
                        {
                            Console.Write('*');
                        }
                        else
                        {
                            Console.Write(key.KeyChar);
                        }
                    }
                    else if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
                    {
                        sb.Length--;
                        Console.Write("\b \b");
                    }
                } while (key.Key != ConsoleKey.Enter);
            }

            Console.ForegroundColor = prevForeground;
            Console.WriteLine();
            return sb.ToString();
        }
    }
}
