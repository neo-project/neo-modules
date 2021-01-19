using Neo.FSNode.Core.Container;
using Neo.FSNode.Core.Netmap;
using Neo.FSNode.Network;
using Neo.FSNode.Services.Object.RangeHash.HasherSource;
using Neo.FSNode.Services.Object.Util;
using Neo.FSNode.Services.ObjectManager.Placement;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.FSNode.Services.Object.RangeHash
{
    public class DistributedHasher
    {
        private Traverser traverser;
        private INetmapSource netmapSource;
        private IContainerSource containerSource;
        private ILocalAddressSource localAddressSource;

        public RangeHashResult Head(RangeHashPrm prm)
        {
            Prepare(prm);
            return Finish(prm);
        }

        private void Prepare(RangeHashPrm prm)
        {
            var nm = netmapSource.GetLatestNetworkMap();
            if (nm is null)
                throw new InvalidOperationException(nameof(Prepare) + " could not get latest network map");
            var container = containerSource.Get(prm.Address.ContainerId);
            if (container is null)
                throw new InvalidOperationException(nameof(Prepare) + " could not get container");
            var builder = new NetmapBuilder(new NetmapSource(nm));
            if (prm.Local)
                builder = new LocalPlacementBuilder(new NetmapSource(nm), localAddressSource);
            traverser = new Traverser
            {
                Builder = builder,
                Policy = container.PlacementPolicy,
                Address = prm.Address,
                FlatSuccess = 1,
            };
        }

        private RangeHashResult Finish(RangeHashPrm prm)
        {
            var result = new RangeHashResult();
            CancellationTokenSource source = default;
            CancellationToken token = source.Token;
            var once_writer = new OnceHashWriter
            {
                TokenSource = source,
                Traverser = traverser,
                Result = result
            };
            while (true)
            {
                var addrs = traverser.Next();
                if (addrs.Length == 0) break;
                var list = new List<Task>();
                foreach (var addr in addrs)
                {
                    //TODO: use workpool
                    list.Add(Task.Factory.StartNew(() =>
                    {
                        IHasherSource hasher;
                        if (addr.IsLocalAddress(localAddressSource))
                        {
                            hasher = new LocalHasherSource();
                        }
                        else
                        {
                            hasher = new RemoteHasherSource();
                        }
                        hasher.HashRange(prm, once_writer.Write);
                    }, token));
                }
                Task.WaitAll(list.ToArray());
            }
            if (!traverser.Success())
                throw new InvalidOperationException(nameof(Finish) + " incomplete object GetRangeHash operation");
            return result;
        }
    }
}
