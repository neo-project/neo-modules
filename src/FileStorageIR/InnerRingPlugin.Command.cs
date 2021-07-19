using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Google.Protobuf;
using Neo.ConsoleService;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Cryptography.Tz;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;
using Neo.FileStorage.InnerRing.Utils.Locode;
using Neo.FileStorage.InnerRing.Utils.Locode.Db;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Plugins;
using Neo.SmartContract.Native;
using Neo.Wallets;
using OHeader = Neo.FileStorage.API.Object.Header;

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
            var cancel = new CancellationTokenSource();

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
            Start(walletProvider.GetWallet());
        }

        [ConsoleCommand("fs container put", Category = "FileStorageService", Description = "Create a container")]
        private void OnPutContainer()
        {
            var host = "http://192.168.130.71:8080";
            var t = File.ReadAllBytes("wallet.key");
            var key = new KeyPair(t).Export().LoadWif();
            var client = new API.Client.Client(key, host);
            var replica = new Replica(2, "");
            var policy = new PlacementPolicy(2, new Replica[] { replica }, null, null);
            var container = new API.Container.Container
            {
                Version = API.Refs.Version.SDKVersion(),
                OwnerId = key.ToOwnerID(),
                Nonce = Guid.NewGuid().ToByteString(),
                BasicAcl = (uint)BasicAcl.PublicBasicRule,
                PlacementPolicy = policy,
            };
            container.Attributes.Add(new Container.Types.Attribute
            {
                Key = "CreatedAt",
                Value = DateTime.UtcNow.ToString(),
            });
            var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            var cid = client.PutContainer(container, context: source.Token).Result;
            Console.WriteLine("create container: " + cid.ToBase58String());
        }

        [ConsoleCommand("fs object put", Category = "FileStorageService", Description = "Create a container")]
        private void OnPutObject(string id)
        {
            var host = "http://192.168.130.71:8080";
            var t = File.ReadAllBytes("wallet.key");
            var key = new KeyPair(t).Export().LoadWif();
            var client = new API.Client.Client(key, host);
            var cid = Neo.FileStorage.API.Refs.ContainerID.FromBase58String(id);
            var payload = Encoding.ASCII.GetBytes("hello");
            var obj = new Neo.FileStorage.API.Object.Object
            {
                Header = new OHeader
                {
                    OwnerId = key.ToOwnerID(),
                    ContainerId = cid,
                },
                Payload = ByteString.CopyFrom(payload),
            };
            obj.ObjectId = obj.CalculateID();
            var source1 = new CancellationTokenSource();
            source1.CancelAfter(TimeSpan.FromMinutes(1));
            var session = client.CreateSession(ulong.MaxValue, context: source1.Token).Result;
            source1.Cancel();
            var source2 = new CancellationTokenSource();
            source2.CancelAfter(TimeSpan.FromMinutes(1));
            var o = client.PutObject(obj, new CallOptions { Ttl = 2, Session = session }, source2.Token).Result;
            Console.WriteLine("ObjectID: " + o.ToBase58String());
        }

        [ConsoleCommand("fs object get", Category = "FileStorageService", Description = "Create a container")]
        private void OnGetObject(string host, string cid, string oid)
        {
            var t = File.ReadAllBytes("wallet.key");
            var key = new KeyPair(t).Export().LoadWif();
            var client = new Client(key, host);
            var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            var o = client.GetObject(new()
            {
                ContainerId = ContainerID.FromBase58String(cid),
                ObjectId = ObjectID.FromBase58String(oid),
            }, false, new CallOptions { Ttl = 1 }, source.Token).Result;
            Console.WriteLine(o.ObjectId.ToBase58String());
            if (o.ObjectType == ObjectType.StorageGroup)
            {
                var sg = StorageGroup.Parser.ParseFrom(o.Payload.ToByteArray());
                Console.WriteLine("StorageGroup:");
                Console.WriteLine($"size: {sg.ValidationDataSize}");
                Console.WriteLine($"members:");
                foreach (var m in sg.Members)
                    Console.WriteLine(m.ToBase58String());
            }
        }

        [ConsoleCommand("fs storage object put", Category = "FileStorageService", Description = "Create a container")]
        private void OnStorageObject(string id, string objid)
        {
            //fs storage object put 9Ujk3Va7AUgY76YeWVhzqsT5pY7uVH8Qt8iwDYDukrWK 5pxDt8uSXvPT2dmb1gNm8cNsGAKJF6f838e2NNagjnFa
            try
            {
                Console.WriteLine("OnStorageObject----step1");
                var host = "http://192.168.130.71:8080";
                var t = File.ReadAllBytes("wallet.key");
                var key = new KeyPair(t).Export().LoadWif();
                var client = new API.Client.Client(key, host);
                var cid = Neo.FileStorage.API.Refs.ContainerID.FromBase58String(id);
                List<ObjectID> oids = new() { ObjectID.FromBase58String(objid) };
                byte[] tzh = null;
                ulong size = 0;
                Console.WriteLine("OnStorageObject----step2");
                foreach (var oid in oids)
                {
                    var address = new API.Refs.Address(cid, oid);
                    var source = new CancellationTokenSource();
                    source.CancelAfter(TimeSpan.FromMinutes(1));
                    var oo = client.GetObject(address, false, new CallOptions { Ttl = 2 }, source.Token).Result;
                    if (tzh is null)
                        tzh = oo.PayloadHomomorphicHash.Sum.ToByteArray();
                    else
                        tzh = TzHash.Concat(new() { tzh, oo.PayloadHomomorphicHash.Sum.ToByteArray() });
                    size += oo.PayloadSize;
                }
                Console.WriteLine("OnStorageObject----step3");
                var epoch = client.Epoch().Result;
                StorageGroup sg = new()
                {
                    ValidationDataSize = size,
                    ValidationHash = new()
                    {
                        Type = ChecksumType.Tz,
                        Sum = ByteString.CopyFrom(tzh)
                    },
                    ExpirationEpoch = epoch + 100,
                };
                sg.Members.AddRange(oids);
                var obj = new Neo.FileStorage.API.Object.Object
                {
                    Header = new OHeader
                    {
                        OwnerId = key.ToOwnerID(),
                        ContainerId = cid,
                        ObjectType = ObjectType.StorageGroup,
                    },
                    Payload = ByteString.CopyFrom(sg.ToByteArray()),
                };
                Console.WriteLine("OnStorageObject----step4");
                obj.ObjectId = obj.CalculateID();
                var source1 = new CancellationTokenSource();
                source1.CancelAfter(TimeSpan.FromMinutes(1));
                var session = client.CreateSession(ulong.MaxValue, context: source1.Token).Result;
                source1.Cancel();
                Console.WriteLine("OnStorageObject----step5");
                var source2 = new CancellationTokenSource();
                source2.CancelAfter(TimeSpan.FromMinutes(1));
                Console.WriteLine("OnStorageObject----step6");
                var o = client.PutObject(obj, new CallOptions { Ttl = 2, Session = session }, source2.Token).Result;
                Console.WriteLine("Storage object ID:" + o.ToBase58String());
            }
            catch (Exception e)
            {
                Console.WriteLine("OnStorageObject----Exception:" + e.Message);
            }
        }

        [ConsoleCommand("fs container delete", Category = "FileStorageService", Description = "Create a container")]
        private void OnRemoveContainer(string id)
        {
            /*
            192.168.130.10 bastion.neofs.devenv
            192.168.130.50 main_chain.neofs.devenv
            192.168.130.81 http.neofs.devenv
            192.168.130.61 ir01.neofs.devenv
            192.168.130.90 morph_chain.neofs.devenv
            192.168.130.82 s3.neofs.devenv
            192.168.130.82 *.s3.neofs.devenv
            192.168.130.71 s01.neofs.devenv
            192.168.130.72 s02.neofs.devenv
            192.168.130.73 s03.neofs.devenv
            192.168.130.74 s04.neofs.devenv
            */
            var host = "http://192.168.130.71:8080";
            var t = File.ReadAllBytes("wallet.key");
            var key = new KeyPair(t).Export().LoadWif();
            var client = new API.Client.Client(key, host);
            var cid = Neo.FileStorage.API.Refs.ContainerID.FromBase58String(id);
            var source = new CancellationTokenSource();
            source.CancelAfter(10000);
            client.DeleteContainer(cid, context: source.Token).Wait();
        }

        [ConsoleCommand("fs container get", Category = "FileStorageService", Description = "Create a container")]
        private void OnGetContainer(string id)
        {
            var host = "http://192.168.130.71:8080";
            var t = File.ReadAllBytes("wallet.key");
            var key = new KeyPair(t).Export().LoadWif();
            var client = new API.Client.Client(key, host);
            var cid = Neo.FileStorage.API.Refs.ContainerID.FromBase58String(id);
            var source = new CancellationTokenSource();
            var container = client.GetContainer(cid, context: source.Token).Result;
            Console.WriteLine("Get container success");
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
