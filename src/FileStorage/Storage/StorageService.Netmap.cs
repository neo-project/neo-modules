using System;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Netmap;

namespace Neo.FileStorage
{
    public sealed partial class StorageService : IDisposable
    {
        private NetmapServiceImpl InitializeNetmap()
        {
            netmapProcessor.AddEpochParser(MorphEvent.NewEpochEvent.ParseNewEpochEvent);
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is MorphEvent.NewEpochEvent e)
                {
                    Interlocked.Exchange(ref CurrentEpoch, e.EpochNumber);
                }
            });
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is MorphEvent.NewEpochEvent e)
                {
                    var ni = NetmapLocalNodeInfo(e.EpochNumber);
                    if (ni is null)
                        Utility.Log(nameof(StorageService), LogLevel.Debug, $"could not update node info, not found in netmap");
                    Interlocked.Exchange(ref LocalNodeInfo, ni);
                }
            });
            return new NetmapServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        StorageNode = this,
                        NetmapService = new()
                        {
                            StorageNode = this,
                        }
                    }
                }
            };
        }

        private NodeInfo NetmapLocalNodeInfo(ulong epoch)
        {
            var nm = morphClient.InvokeEpochSnapshot(epoch);
            Node node = null;
            foreach (var n in nm.Nodes)
            {
                if (n.PublicKey.SequenceEqual(key.PublicKey()))
                {
                    node = n;
                    break;
                }
            }
            return node?.Info;
        }
    }
}
