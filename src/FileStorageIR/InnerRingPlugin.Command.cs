using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Neo.ConsoleService;
using Neo.FileStorage.InnerRing.Utils.Locode;
using Neo.FileStorage.InnerRing.Utils.Locode.Db;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Plugins;
using Neo.SmartContract.Native;

namespace Neo.FileStorage.InnerRing
{
    /// <summary>
    /// The entrance of the FSNode program.
    /// Built-in an innering service to process notification events related to FS when the block is persisted.
    /// </summary>
    public partial class InnerRingPlugin : Plugin, IPersistencePlugin
    {
        public const string ReourcePath = "./Resources/";
        public const string DefaultTargetPath = "./Data_UNLOCODE";
        public LocalNode LocalNode;
        private readonly CancellationTokenSource _shutdownTokenSource = new();

        protected string ReadLine()
        {
            Task<string> readLineTask = Task.Run(() => Console.ReadLine());

            try
            {
                readLineTask.Wait(_shutdownTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            return readLineTask.Result;
        }

        private static void WriteLineWithoutFlicker(string message = "", int maxWidth = 80)
        {
            if (message.Length > 0) Console.Write(message);
            var spacesToErase = maxWidth - message.Length;
            if (spacesToErase < 0) spacesToErase = 0;
            Console.WriteLine(new string(' ', spacesToErase));
        }

        [ConsoleCommand("fs show state", Category = "FileStorageService", Description = "Show side chain node height and connection")]
        private void OnNodeHeight()
        {
            using var cancel = new CancellationTokenSource();

            Console.CursorVisible = false;
            Console.Clear();

            Task broadcast = Task.Run(async () =>
            {
                while (!cancel.Token.IsCancellationRequested)
                {
                    MorphSystem.LocalNode.Tell(Message.Create(MessageCommand.Ping, PingPayload.Create(NativeContract.Ledger.CurrentIndex(MorphSystem.StoreView))));
                    await Task.Delay(morphProtocolSettings.TimePerBlock, cancel.Token);
                }
            });
            Task task = Task.Run(async () =>
            {
                int maxLines = 0;
                while (!cancel.Token.IsCancellationRequested)
                {
                    uint height = NativeContract.Ledger.CurrentIndex(MorphSystem.StoreView);
                    uint headerHeight = MorphSystem.HeaderCache.Last?.Index ?? height;

                    Console.SetCursorPosition(0, 0);
                    WriteLineWithoutFlicker($"block: {height}/{headerHeight}  connected: {LocalNode.ConnectedCount}  unconnected: {LocalNode.UnconnectedCount}", Console.WindowWidth - 1);

                    int linesWritten = 1;
                    foreach (RemoteNode node in LocalNode.GetRemoteNodes().OrderByDescending(u => u.LastBlockIndex).Take(Console.WindowHeight - 2).ToArray())
                    {
                        Console.WriteLine(
                            $"  ip: {node.Remote.Address,-15}\tport: {node.Remote.Port,-5}\tlisten: {node.ListenerTcpPort,-5}\theight: {node.LastBlockIndex,-7}");
                        linesWritten++;
                    }

                    maxLines = Math.Max(maxLines, linesWritten);

                    while (linesWritten < maxLines)
                    {
                        WriteLineWithoutFlicker("", Console.WindowWidth - 1);
                        maxLines--;
                    }

                    await Task.Delay(500, cancel.Token);
                }
            });
            ReadLine();
            cancel.Cancel();
            try { Task.WaitAll(task, broadcast); } catch { }
            Console.WriteLine();
            Console.CursorVisible = true;
        }

        [ConsoleCommand("fs start ir", Category = "FileStorageService", Description = "Start as inner ring node")]
        private void OnStartIR()
        {
            Start();
        }

        [ConsoleCommand("fs generate", Category = "FileStorageService", Description = "generate UN/LOCODE database for NeoFS using specified paths")]
        private void OnGenerate(string tableInPaths, string tableSubDivPath, string airportsPath, string countriesPath, string continentsPath, string targetDBPath)
        {
            CSVTable locodeDB = new(tableInPaths.Split(","), tableSubDivPath);
            AirportsDB airportsDB = new()
            {
                AirportsPath = airportsPath,
                CountriesPath = countriesPath
            };
            ContinentDB continentDB = new()
            {
                Path = continentsPath
            };
            StorageDB targetDb = new(targetDBPath);
            targetDb.FillDatabase(locodeDB, airportsDB, continentDB);
        }

        [ConsoleCommand("fs generate default", Category = "FileStorageService", Description = "generate UN/LOCODE database for NeoFS using default resources")]
        private void OnGenerate()
        {
            string[] tableInPaths = new string[]
            {
                ReourcePath + "2020-2 UNLOCODE CodeListPart1.csv",
                ReourcePath + "2020-2 UNLOCODE CodeListPart2.csv",
                ReourcePath + "2020-2 UNLOCODE CodeListPart3.csv",
            };
            string tableSubDivPath = ReourcePath + "2020-2 SubdivisionCodes.csv";
            string airportsPath = ReourcePath + "airports.dat";
            string countriesPath = ReourcePath + "countries.dat";
            string continentsPath = ReourcePath + "continents.geojson";
            CSVTable locodeDB = new(tableInPaths, tableSubDivPath);
            AirportsDB airportsDB = new()
            {
                AirportsPath = airportsPath,
                CountriesPath = countriesPath
            };
            ContinentDB continentDB = new()
            {
                Path = continentsPath
            };
            StorageDB targetDb = new(DefaultTargetPath);
            targetDb.FillDatabase(locodeDB, airportsDB, continentDB);
        }
    }
}
