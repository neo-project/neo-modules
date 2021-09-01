using Akka.Actor;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Storage.Core.Container;
using Neo.FileStorage.Storage.Services.Object.Head;
using Neo.FileStorage.Storage.Services.Replicate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Storage.Services.Police
{
    public class Policer : UntypedActor
    {
        public class Args
        {
            public const int DefaultWorkScope = 100;
            public const int DefaultExpandRate = 10;
            public static readonly TimeSpan DefaultHeadTimeout = TimeSpan.FromSeconds(5);
            public int ExpandRate { get; init; } = DefaultExpandRate;
            public int WorkScope { get; set; } = DefaultWorkScope;
            public TimeSpan HeadTimeout { get; init; } = DefaultHeadTimeout;
            public ILocalInfoSource LocalInfo { get; init; }
            public IContainerSoruce ContainerSoruce { get; init; }
            public IActorRef ReplicatorRef { get; init; }
            public IObjectListSource LocalStorage { get; init; }
            public IPlacementBuilder PlacementBuilder { get; init; }
            public IRemoteHeader RemoteHeader { get; init; }
            public Action<FSAddress> RedundantCopyCallback { get; init; }
        }

        private class PoliceTask
        {
            public CancellationTokenSource Source;
            public Task Task;
            public int Undone;
        }

        public class Trigger { }
        private int workScope;
        private readonly Args args;
        private readonly PoliceTask prevTask = new();

        public Policer(Args a)
        {
            args = a;
            workScope = args.WorkScope;
        }

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
            prevTask.Source?.Cancel();
            prevTask.Source?.Dispose();
            HandleTask();
        }

        public void HandleTask()
        {
            int delta;
            if (0 < prevTask.Undone)
                delta = -prevTask.Undone;
            else
                delta = workScope * args.ExpandRate / 100;
            var addrs = Select(workScope + delta);
            if (workScope + delta < addrs.Count)
                workScope += delta;
            prevTask.Undone = addrs.Count;
            prevTask.Source = new();
            var token = prevTask.Source.Token;
            prevTask.Task = Task.Run(() =>
            {
                foreach (var addr in addrs)
                {
                    if (token.IsCancellationRequested) return;
                    ProcessObject(addr);
                    prevTask.Undone--;
                }
            }, token);
        }

        private List<FSAddress> Select(int limit)
        {
            var res = args.LocalStorage.List((ulong)limit);
            return res.Take(limit).ToList();
        }

        private void ProcessNodes(FSAddress address, List<Node> nodes, uint shortage)
        {
            RemoteHeadPrm prm = new()
            {
                Address = address,
            };
            bool redundantLocalCopy = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                List<Network.Address> addrs;
                try
                {
                    addrs = nodes[i].NetworkAddresses.Select(p => Network.Address.FromString(p)).ToList();
                }
                catch
                {
                    continue;
                }
                if (addrs.Intersect(args.LocalInfo.Addresses).Any())
                {
                    if (shortage == 0)
                        redundantLocalCopy = true;
                    else
                        shortage--;
                }
                else if (0 < shortage)
                {
                    prm.Addresses = addrs;
                    using CancellationTokenSource source = new(args.HeadTimeout);
                    try
                    {
                        _ = args.RemoteHeader.Head(prm, source.Token);
                    }
                    catch
                    {
                        continue;
                    }
                    shortage--;
                }
                nodes.RemoveAt(i);
                i--;
            }
            if (0 < shortage)
            {
                args.ReplicatorRef.Tell(new Replicator.Task
                {
                    Quantity = shortage,
                    Address = address,
                    Nodes = nodes,
                });
            }
            else if (redundantLocalCopy && args.RedundantCopyCallback is not null)
            {
                args.RedundantCopyCallback(address);
            }
        }

        private void ProcessObject(FSAddress address)
        {
            var container = args.ContainerSoruce.GetContainer(address.ContainerId);
            var policy = container.PlacementPolicy;
            var nodes = args.PlacementBuilder.BuildPlacement(address, policy);
            var replicas = policy.Replicas;
            for (int i = 0; i < nodes.Count; i++)
                ProcessNodes(address, nodes[i], replicas[i].Count);
        }

        public void Dispose()
        {
            prevTask.Source?.Cancel();
            prevTask.Source?.Dispose();
        }

        public static Props Props(Args c)
        {
            return Akka.Actor.Props.Create(() => new Policer(c));
        }
    }
}
