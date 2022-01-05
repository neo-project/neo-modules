using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Storage.Services.Netmap;
using System;
using System.Linq;
using System.Threading;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IDisposable
    {
        public const ulong DefaultBootstrapInterval = 2;
        private ulong startEpoch;
        private long needBootstrap = 1;

        private NetmapServiceImpl InitializeNetmap()
        {
            startEpoch = morphInvoker.Epoch();
            netmapProcessor.AddEpochParser(NewEpochEvent.ParseNewEpochEvent);
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is NewEpochEvent e)
                {
                    Utility.Log(nameof(StorageService), LogLevel.Debug, $"update current epoch, epoch={e.EpochNumber}");
                    Interlocked.Exchange(ref currentEpoch, e.EpochNumber);
                }
            });
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is NewEpochEvent e)
                {
                    var ni = NetmapLocalNodeInfo(e.EpochNumber);
                    if (ni is null)
                    {
                        lock (localNodeInfo)
                        {
                            localNodeInfo.State = NodeInfo.Types.State.Offline;
                        }
                        Utility.Log(nameof(StorageService), LogLevel.Debug, $"could not update node info, not found in netmap");
                        return;
                    }
                    Utility.Log(nameof(StorageService), LogLevel.Debug, $"update local node info, info={ni}");
                    Interlocked.Exchange(ref localNodeInfo, ni);
                }
            });
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is NewEpochEvent e)
                {
                    if (0 == Interlocked.Read(ref needBootstrap)) return;
                    if ((e.EpochNumber - startEpoch) % Settings.Default.BootstrapInterval == 0)
                    {
                        var ni = Settings.Default.LocalNodeInfo.Clone();
                        ni.State = NodeInfo.Types.State.Online;
                        try
                        {
                            morphInvoker.AddPeer(ni);
                        }
                        catch (Exception ex)
                        {
                            Utility.Log(nameof(StorageService), LogLevel.Warning, $"could not add peer when rebootstrap, error={ex.Message}");
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
                            LocalInfoSource = this,
                            MorphInvoker = morphInvoker,
                        }
                    }
                }
            };
        }

        public NodeInfo NetmapLocalNodeInfo(ulong epoch)
        {
            var nm = netmapCache.GetNetMapByEpoch(epoch);
            var pk = key.PublicKey();
            foreach (var n in nm.Nodes)
            {
                if (n.PublicKey.SequenceEqual(pk))
                {
                    return n.Info;
                }
            }
            return null;
        }
    }
}
