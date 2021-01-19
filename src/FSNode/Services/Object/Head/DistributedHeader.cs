using Neo.FSNode.Network;
using Neo.FSNode.Core.Container;
using Neo.FSNode.Core.Netmap;
using Neo.FSNode.LocalObjectStorage.LocalStore;
using Neo.FSNode.Network.Cache;
using Neo.FSNode.Services.Object.Head.HeaderSource;
using Neo.FSNode.Services.Object.Util;
using Neo.FSNode.Services.ObjectManager.Placement;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.FSNode.Services.Object.Head
{
    public class DistributedHeader
    {
        private Storage localStorage;
        private INetmapSource netmapSource;
        private IContainerSource containerSource;
        private ILocalAddressSource localAddressSource;
        public ClientCache ClientCache;
        private Traverser traverser;
        public KeyStorage KeyStorage;

        public HeadResult Head(HeadPrm prm)
        {
            Prepare(prm);
            return Finish(prm);
        }

        private void Prepare(HeadPrm prm)
        {
            var netmap = netmapSource.GetLatestNetworkMap();
            var container = containerSource.Get(prm.Address.ContainerId);
            var builder = new NetmapBuilder(new NetmapSource(netmap));
            if (prm.Local)
                builder = new LocalPlacementBuilder(new NetmapSource(netmap), localAddressSource);
            traverser = new Traverser
            {
                Builder = builder,
                Policy = container.PlacementPolicy,
                Address = prm.Address,
                FlatSuccess = 1,
            };
        }

        private HeadResult Finish(HeadPrm prm)
        {
            var resp = new HeadResult();
            CancellationTokenSource source = default;
            CancellationToken token = source.Token;
            var writer = new OnceHeaderWriter
            {
                Traverser = traverser,
                Result = resp,
                TokenSource = source,
            };
            while (true)
            {
                var addrs = traverser.Next();
                if (addrs.Length == 0) break;
                var tasks = new Task[addrs.Length];
                for (int i = 0; i < addrs.Length; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        IHeaderSource header_source = null;
                        if (addrs[i].IsLocalAddress(localAddressSource))
                        {
                            header_source = new LocalHeaderSource
                            {
                                LocalStorage = localStorage
                            };
                        }
                        else
                        {
                            header_source = new RemoteHeaderSource
                            {
                                KeyStorage = KeyStorage,
                                ClientCache = ClientCache,
                                Node = addrs[i],
                                SessionToken = prm.SessionToken,
                                BearerToken = prm.BearerToken,
                            };
                        }
                        writer.Write(header_source.Head(prm.Address));
                    });
                }
                Task.WaitAll(tasks);
            }
            if (!traverser.Success()) throw new InvalidOperationException(nameof(DistributedHeader) + " incomplete object header operation");
            return resp;
        }
    }
}
