// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.Plugins.RestServer.Controllers.v1;
using System.Text;

namespace Neo.Plugins.RestServer
{
    public partial class RestServerPlugin
    {
        [ConsoleCommand("rest sessions", Category = "RestServer Commands", Description = "Shows all active wallet sessions.")]
        private void OnShowWalletSessions()
        {

            if (WalletController.WalletSessions.Any())
                ConsoleHelper.Info("---------", "Sessions", "---------");

            foreach (var (key, value) in WalletController.WalletSessions)
            {
                TimeSpan expires = value.Expires.Subtract(DateTime.Now);
                var wallet = value.Wallet;
                ConsoleHelper.Info("   Name: ", wallet.Name ?? "\"\"");
                ConsoleHelper.Info("   Path: ", wallet.Path);
                ConsoleHelper.Info("Version: ", wallet.Version.ToString());
                ConsoleHelper.Info("Session: ", key.ToString("n"));
                ConsoleHelper.Info("Expires: ", Math.Round(expires.TotalSeconds, 0).ToString(), " second(s).");

                if (WalletController.WalletSessions.Count > 1)
                    ConsoleHelper.Info();
            }

            if (WalletController.WalletSessions.Any())
                ConsoleHelper.Info("--------------------------");

            ConsoleHelper.Info("  Total: ", WalletController.WalletSessions.Count.ToString(), " session(s).");
        }

        [ConsoleCommand("rest kill", Category = "RestServer Commands", Description = "Kills an active wallet session.")]
        private void OnKillWalletSession(string sessionId)
        {
            if (Guid.TryParse(sessionId, out var session) == false)
            {
                ConsoleHelper.Warning("Invalid session id.");
                return;
            }

            ConsoleHelper.Info($"You are about to kill ", session.ToString("n"), " session!");
            var answer = ReadUserInput($"Are you sure?");
            if (answer.Equals("yes", StringComparison.InvariantCultureIgnoreCase) || answer.Equals("y", StringComparison.InvariantCultureIgnoreCase))
            {
                if (WalletController.WalletSessions.TryRemove(session, out _))
                    ConsoleHelper.Info("Session ", session.ToString("n"), " has terminated.");
                else
                    ConsoleHelper.Error($"Session {session:n} could not be terminated. Try again later.");
            }
        }

        [ConsoleCommand("rest killall", Category = "RestServer Commands", Description = "Kills all active wallet sessions.")]
        public void OnKillallWalletSessions()
        {
            if (WalletController.WalletSessions.Any() == false)
            {
                ConsoleHelper.Info("No ", "active", " sessions.");
                return;
            }

            var answer = ReadUserInput($"Kill all active wallet sessions?");
            if (answer.Equals("yes", StringComparison.InvariantCultureIgnoreCase) || answer.Equals("y", StringComparison.InvariantCultureIgnoreCase))
            {
                foreach (var (session, _) in WalletController.WalletSessions)
                {
                    if (WalletController.WalletSessions.TryRemove(session, out _))
                        ConsoleHelper.Info("Session ", session.ToString("n"), " has terminated.");
                    else
                        ConsoleHelper.Error($"Session {session:n} could not be terminated. Try again later.");
                }
            }
        }

        private string ReadUserInput(string prompt, bool password = false)
        {
            const string t = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            var sb = new StringBuilder();

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
                ConsoleKeyInfo key;
                do
                {
                    key = Console.ReadKey(true);

                    if (t.IndexOf(key.KeyChar) != -1)
                    {
                        sb.Append(key.KeyChar);
                        Console.Write(password ? '*' : key.KeyChar);
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
