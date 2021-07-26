using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Akka.Actor;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.InnerRing.Services.Audit;
using Neo.FileStorage.Listen.Event;
using Neo.FileStorage.Listen.Event.Morph;
using static Neo.FileStorage.InnerRing.Services.Audit.Manager;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class AuditContractProcessor : BaseProcessor, IDisposable
    {
        public override string Name => "AuditContractProcessor";
        public int SearchTimeout => Settings.Default.SearchTimeout;
        public IActorRef TaskManager;
        public CancellationTokenSource prevAuditCanceler = new();
        public IFSClientCache ClientCache;
        private ECDsa key;

        private ECDsa Key
        {
            get
            {
                if (key is null)
                    key = MorphInvoker.Wallet.GetAccounts().First().GetKey().PrivateKey.LoadPrivateKey();
                return key;
            }
        }

        public void HandleNewAuditRound(ContractEvent morphEvent)
        {
            StartEvent startEvent = (StartEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, $"new round of audit, epoch={startEvent.epoch}");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new System.Threading.Tasks.Task(() => ProcessStartAudit(startEvent.epoch)) });
        }

        public void ProcessStartAudit(ulong epoch)
        {
            prevAuditCanceler.Cancel();
            prevAuditCanceler.Dispose();
            int skipped = (int)TaskManager.Ask(new ResetMessage()).Result;
            if (skipped > 0) Utility.Log(Name, LogLevel.Info, $"some tasks from previous epoch are skipped, amount={skipped}");
            ContainerID[] containers;
            try
            {
                containers = SelectContainersToAudit(epoch);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"container selection failure, error={e}");
                return;
            }
            Utility.Log(Name, LogLevel.Info, $"select containers for audit, amount={containers.Length}");
            NetMap nm;
            try
            {
                nm = MorphInvoker.GetNetMapByDiff(0);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't fetch network map, error={e}");
                return;
            }
            for (int i = 0; i < containers.Length; i++)
            {
                Container cnr;
                try
                {
                    cnr = MorphInvoker.GetContainer(containers[i]).Container;
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Error, $"can't get container info, ignore, cid={containers[i]}, error={e}");
                    continue;
                }
                var pivot = containers[i].Value.ToByteArray();
                List<List<Node>> nodes;
                try
                {
                    nodes = nm.GetContainerNodes(cnr.PlacementPolicy, pivot);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Error, $"can't build placement for container, ignore, cid={containers[i]}, error={e}");
                    continue;
                }
                Random rand = new();
                var n = nodes.Flatten().OrderBy(c => rand.Next()).ToArray();
                var storageGroups = FindStorageGroups(containers[i], n);
                Utility.Log(Name, LogLevel.Info, $"select storage groups for audit, cid={containers[i]}, count={storageGroups.Length}");

                prevAuditCanceler = new CancellationTokenSource();
                AuditTask auditTask = new()
                {
                    Reporter = new EpochAuditReporter()
                    {
                        Epoch = epoch,
                        Reporter = State
                    },
                    Cancellation = prevAuditCanceler.Token,
                    ContainerID = containers[i],
                    SGList = storageGroups.ToList(),
                    Container = cnr,
                    ContainerNodes = nodes,
                    Netmap = nm
                };
                TaskManager.Tell(auditTask);
            }
        }

        private ContainerID[] SelectContainersToAudit(ulong epoch)
        {
            List<ContainerID> containers;
            try
            {
                containers = MorphInvoker.ListContainers(new OwnerID());
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"can't get list of containers to start audit, error={e}");
            }
            containers.Sort((x, y) => x.ToBase58String().CompareTo(y.ToBase58String()));
            var ind = State.InnerRingIndex();
            var irSize = State.InnerRingSize();
            if (ind < 0 || ind >= irSize) throw new InvalidOperationException("node is not in the inner ring list");
            return Select(containers.ToArray(), epoch, (ulong)ind, (ulong)irSize);
        }

        private ContainerID[] Select(ContainerID[] containIds, ulong epoch, ulong index, ulong size)
        {
            ulong a;
            ulong b = 0;
            ulong ln = (ulong)containIds.Length;
            ulong pivot = ln % size;
            ulong delta = ln / size;
            index = (index + epoch) % size;
            if (index < pivot)
            {
                a = delta + 1;
            }
            else
            {
                a = delta;
                b = pivot;
            }
            var from = a * index + b;
            var to = a * (index + 1) + b;
            return containIds.Skip((int)from).Take((int)(to - from)).ToArray();
        }

        private ObjectID[] FindStorageGroups(ContainerID cid, Node[] shuffled)
        {
            List<ObjectID> sg = new();
            for (int i = 0; i < shuffled.Length; i++)
            {
                string pairs = $"cid={cid.ToBase58String()},";
                pairs += $" address={shuffled[i].Info.Address},";
                pairs += $" try={i},";
                pairs += $" total_tries={shuffled}";
                Utility.Log(Name, LogLevel.Info, pairs);
                Network.Address address;
                try
                {
                    address = Network.Address.FromString(shuffled[i].NetworkAddress);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Warning, $"can't parse remote address, error={e}");
                    continue;
                };
                IFSClient cli;
                try
                {
                    cli = ClientCache.Get(address);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Warning, $"can't setup remote connection, error={e}");
                    continue;
                };
                SearchFilters searchFilters = new();
                searchFilters.AddTypeFilter(MatchType.StringEqual, ObjectType.StorageGroup);
                try
                {
                    var source = new CancellationTokenSource();
                    source.CancelAfter(TimeSpan.FromMinutes(1));
                    List<ObjectID> result = cli.SearchObject(cid, searchFilters, new CallOptions() { Key = Key }, context: source.Token).Result;
                    sg.AddRange(result);
                    break;
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Warning, $"error in storage group search, error={e}");
                    continue;
                }
            }
            return sg.ToArray();
        }

        public void Dispose()
        {
            prevAuditCanceler?.Cancel();
            prevAuditCanceler?.Dispose();
            key?.Dispose();
        }
    }
}
