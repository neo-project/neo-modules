using System;
using System.Linq;
using System.Threading;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Storage.Services.Netmap;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IDisposable
    {
        private const int reBootstrapInterval = 2;
        private ulong startEpoch;
        private long reBoostrapTurnedOff = 0;

        private NetmapServiceImpl InitializeNetmap()
        {
            startEpoch = morphInvoker.Epoch();
            netmapProcessor.AddEpochParser(NewEpochEvent.ParseNewEpochEvent);
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is NewEpochEvent e)
                {
                    Interlocked.Exchange(ref currentEpoch, e.EpochNumber);
                }
            });
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is NewEpochEvent e)
                {
                    var ni = NetmapLocalNodeInfo(e.EpochNumber);
                    if (ni is null)
                        Utility.Log(nameof(StorageService), LogLevel.Debug, $"could not update node info, not found in netmap");
                    Interlocked.Exchange(ref localNodeInfo, ni);
                }
            });
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is NewEpochEvent e)
                {
                    if (0 < Interlocked.Read(ref reBoostrapTurnedOff)) return;
                    if ((e.EpochNumber - startEpoch) % reBootstrapInterval == 0)
                    {
                        var ni = localNodeInfo.Clone();
                        ni.State = API.Netmap.NodeInfo.Types.State.Online;
                        try
                        {
                            morphInvoker.AddPeer(ni);
                        }
                        catch (Exception exp)
                        {
                            Utility.Log(nameof(StorageService), LogLevel.Debug, $"could not add peer when rebootstrap, error: {exp}");
                        }
                    }
                }
            });
            return new NetmapServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        EpochSource = this,
                        NetmapService = new()
                        {
                            EpochSource = this,
                        }
                    }
                }
            };
        }

        private NodeInfo NetmapLocalNodeInfo(ulong epoch)
        {
            var nm = morphInvoker.GetNetMapByEpoch(epoch);
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
