using System;
using System.Threading;
using Neo.FileStorage.Morph.Event;
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
    }
}
