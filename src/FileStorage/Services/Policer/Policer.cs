using Akka.Actor;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using V2Address = Neo.FileStorage.API.Refs.Address;
using Neo.FileStorage.Core.Container;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Network;
using static Neo.FileStorage.Network.Address;
using Neo.FileStorage.Services.Object.Head.HeaderSource;
using Neo.FileStorage.Services.ObjectManager.Placement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.FileStorage.Services.Policer
{
    public class Policer : UntypedActor
    {
        public class Trigger { }
        public TimeSpan HeadTimeout;
        public ILocalAddressSource LocalAddressSource;
        public Replicator.Replicator Replicator;
        private StorageEngine localStorage;
        public IContainerSource ContainerSource;
        public IPlacementBuilder PlacementBuilder;
        public RemoteHeaderSource remoteHeaderSource;
        private static SearchFilters jobFilters;
        private readonly List<Task> tasks = new List<Task>();
        public int Undone;
        public int WaitCount;
        public int WorkScope;
        public int ExpandRate;

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Trigger _:
                    OnTrigger();
                    break;
            }
        }

        private void OnTrigger()
        {
            Cancel();
            Wait();
            AddTask();
        }

        public void Cancel()
        {

        }

        public void Wait()
        {
            Task.WaitAll(tasks.ToArray());
        }

        private List<V2Address> Select(int limit)
        {
            // TODO: optimize the logic for selecting objects
            // We can prioritize objects for migration, newly arrived objects, etc.
            // It is recommended to make changes after updating the metabase
            var res = localStorage.List((ulong)limit);
            if (res.Count < limit) return res.ToList();

            return res.Take(limit).ToList();
        }

        private SearchFilters GetJobFilter()
        {
            if (jobFilters.Filters.Length == 0)
                jobFilters.AddPhyFilter();

            return jobFilters;
        }

        private void ProcessNode(V2Address address, List<Node> nodes, uint shortage)
        {
            var nlist = nodes.ToList();
            foreach (var n in nlist)
            {
                if (shortage == 0) break;
                var net_address = n.NetworkAddress;
                var node = AddressFromString(net_address);
                if (node.IsLocalAddress(LocalAddressSource))
                {
                    shortage--;
                }
                else
                {
                    remoteHeaderSource.Node = node;
                    var header = remoteHeaderSource.Head(address);
                    if (header is null)
                    {
                        //TODO: fix
                        continue;
                    }
                    shortage--;
                }
                nlist.Remove(n);
            }
            if (0 < shortage)
            {
                //Replicator tell
            }
        }

        private void ProcessObject(V2Address address)
        {
            var container = ContainerSource.Get(address.ContainerId);
            if (container is null) return;
            var policy = container.PlacementPolicy;
            var nodes = PlacementBuilder.BuildPlacement(address, policy);
            var replicas = policy.Replicas;
            for (int i = 0; i < nodes.Count; i++)
                ProcessNode(address, nodes[i], replicas[i].Count);
        }

        public void AddTask()
        {
            tasks.Add(Task.Run(() =>
            {
                int delta;
                if (0 < Undone)
                    delta = -Undone;
                else
                    delta = WorkScope * ExpandRate / 100;
                var addrs = Select(WorkScope + delta);
                if (WorkScope + delta < addrs.Count)
                    WorkScope += delta;
                Undone = addrs.Count;
                foreach (var addr in addrs)
                {
                    ProcessObject(addr);
                    Undone--;
                }
            }));
        }
    }
}
