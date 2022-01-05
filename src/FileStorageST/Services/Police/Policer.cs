using Akka.Actor;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Storage.Core.Container;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.Storage.Services.Object.Head;
using Neo.FileStorage.Storage.Services.Replicate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Neo.FileStorage.Storage.Core.Container.Helper;
using FSAddress = Neo.FileStorage.API.Refs.Address;
using FSContainer = Neo.FileStorage.API.Container.Container;

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
            public IContainerSource ContainerSoruce { get; init; }
            public IActorRef ReplicatorRef { get; init; }
            public IObjectListSource LocalStorage { get; init; }
            public IObjectInhumer ObjectInhumer { get; init; }
            public IPlacementBuilder PlacementBuilder { get; init; }
            public IRemoteHeader RemoteHeader { get; init; }
            public Action<FSAddress> RedundantCopyCallback { get; init; }
        }

        private class PoliceTask
        {
            public CancellationTokenSource cancellationSource;
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
            prevTask.cancellationSource?.Cancel();
            prevTask.cancellationSource?.Dispose();
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
            if (workScope + delta <= addrs.Count)
                workScope += delta;
            prevTask.Undone = addrs.Count;
            prevTask.cancellationSource = new();
            var token = prevTask.cancellationSource.Token;
            prevTask.Task = Task.Run(() =>
            {
                foreach (var addr in addrs)
                {
                    if (token.IsCancellationRequested) return;
                    ProcessObject(addr, token);
                    prevTask.Undone--;
                }
            }, token);
            Utility.Log(nameof(Policer), LogLevel.Debug, $"start police task, limit={workScope}, count={addrs.Count}");
        }

        private List<FSAddress> Select(int limit)
        {
            var res = args.LocalStorage.List((ulong)limit);
            return res.Take(limit).ToList();
        }

        private void ProcessNodes(FSAddress address, List<Node> nodes, uint shortage, CancellationToken cancellation)
        {
            RemoteHeadPrm prm = new()
            {
                Address = address,
            };
            bool redundantLocalCopy = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (cancellation.IsCancellationRequested) return;
                if (args.LocalInfo.PublicKey.SequenceEqual(nodes[i].PublicKey))
                {
                    if (shortage == 0)
                        redundantLocalCopy = true;
                    else
                        shortage--;
                }
                else if (0 < shortage)
                {
                    prm.Node = nodes[i].Info;
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
                Utility.Log(nameof(Policer), LogLevel.Debug, $"shortage of object copies detected, address={address.String()}, shortage={shortage}");
                args.ReplicatorRef.Tell(new Replicator.Task
                {
                    Quantity = shortage,
                    Address = address,
                    Nodes = nodes,
                });
            }
            else if (redundantLocalCopy && args.RedundantCopyCallback is not null)
            {
                Utility.Log(nameof(Policer), LogLevel.Debug, $"redundant local object copy detected, address={address.String()}, shortage={shortage}");
                args.RedundantCopyCallback(address);
            }
        }

        private void ProcessObject(FSAddress address, CancellationToken cancellation)
        {
            FSContainer container;
            try
            {
                container = args.ContainerSoruce.GetContainer(address.ContainerId)?.Container;
            }
            catch (Exception e)
            {
                Utility.Log(nameof(Policer), LogLevel.Warning, $"could not get container, address={address.String()}, error={e.Message}");
                if (e.Message == ContainerNotFoundError)
                {
                    try
                    {
                        args.ObjectInhumer.Inhume(null, address);
                    }
                    catch (Exception ex)
                    {
                        Utility.Log(nameof(Policer), LogLevel.Error, $"could not inhume object with missing container, address={address.String()}, error={ex.Message}");
                    }
                }
                return;
            }
            var policy = container.PlacementPolicy;
            List<List<Node>> nodes;
            try
            {
                nodes = args.PlacementBuilder.BuildPlacement(address, policy);
            }
            catch (Exception e)
            {
                Utility.Log(nameof(Policer), LogLevel.Error, $"could not build placement for object, address={address.String()}, error={e.Message}");
                return;
            }
            var replicas = policy.Replicas;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (cancellation.IsCancellationRequested) return;
                ProcessNodes(address, nodes[i], replicas[i].Count, cancellation);
            }
        }

        public void Dispose()
        {
            prevTask.cancellationSource?.Cancel();
            prevTask.cancellationSource?.Dispose();
        }

        public static Props Props(Args c)
        {
            return Akka.Actor.Props.Create(() => new Policer(c));
        }
    }
}
