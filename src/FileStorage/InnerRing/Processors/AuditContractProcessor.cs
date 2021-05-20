using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Audit;
using static Neo.FileStorage.Morph.Event.MorphEvent;
using static Neo.FileStorage.Services.Audit.Manager;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class AuditContractProcessor : BaseProcessor
    {
        public override string Name => "AuditContractProcessor";
        public ulong SearchTimeout => Settings.Default.SearchTimeout;
        public IActorRef TaskManager;
        public INeoFSClientCache ClientCache;
        public Action prevAuditCanceler = new Action(() => { });

        public void HandleNewAuditRound(IContractEvent morphEvent)
        {
            StartEvent startEvent = (StartEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, string.Format("new round of audit,epoch:{0}", startEvent.epoch));
            WorkPool.Tell(new NewTask() { Process = Name, Task = new System.Threading.Tasks.Task(() => ProcessStartAudit(startEvent.epoch)) });
        }

        public void ProcessStartAudit(ulong epoch)
        {
            Utility.Log(Name, LogLevel.Info, string.Format("epoch:{0}", epoch));
            prevAuditCanceler();
            int skipped = (int)TaskManager.Ask(new ResetMessage()).Result;
            if (skipped > 0) Utility.Log(Name, LogLevel.Info, string.Format("some tasks from previous epoch are skipped,amount{0}", skipped));
            ContainerID[] containers;
            try
            {
                containers = SelectContainersToAudit(epoch);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("container selection failure,error {0}", e.Message));
                return;
            }
            Utility.Log(Name, LogLevel.Info, string.Format("select containers for audit,amount {0}", containers.Length));
            NetMap nm;
            try
            {
                nm = MorphCli.InvokeSnapshot(0);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("can't fetch network map,error {0}", e.Message));
                return;
            }
            for (int i = 0; i < containers.Length; i++)
            {
                Container cnr;
                try
                {
                    cnr = MorphCli.InvokeGetContainer(containers[i]);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Error, string.Format("can't get container info, ignore,cid:{0},error:{1}", containers[i], e.Message));
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
                    Utility.Log(Name, LogLevel.Error, string.Format("can't build placement for container, ignore,cid:{0},error:{1}", containers[i], e.Message));
                    continue;
                }
                var n = nodes.Flatten().ToArray();
                n = n.OrderBy(c => Guid.NewGuid()).ToArray();
                var storageGroups = FindStorageGroups(containers[i], n);
                Utility.Log(Name, LogLevel.Info, string.Format("select storage groups for audit,cid:{0},amount:{1}", containers[i], storageGroups.Length));

                var source = new CancellationTokenSource();
                AuditTask auditTask = new()
                {
                    Reporter = new EpochAuditReporter()
                    {
                        epoch = epoch,
                        reporter = State
                    },
                    Cancellation = source.Token,
                    ContainerID = containers[i],
                    SGList = storageGroups.ToList(),
                    Container = cnr,
                    ContainerNodes = nodes,
                    Netmap = nm
                };
                try
                {
                    TaskManager.Tell(auditTask);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Info, string.Format("could not push audit task,errot:{0}", e.Message));
                }
            }
        }

        private ContainerID[] SelectContainersToAudit(ulong epoch)
        {
            List<ContainerID> containers;
            try
            {
                containers = MorphCli.InvokeGetContainerList(new OwnerID());
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("can't get list of containers to start audit,error {0}", e.Message));
            }
            Utility.Log(Name, LogLevel.Info, string.Format("container listing finished,total amount {0}", containers.Count));
            containers.Sort((x, y) => x.ToBase58String().CompareTo(y.ToBase58String()));
            var ind = State.InnerRingIndex();
            var irSize = State.InnerRingSize();
            if (ind < 0 || ind >= irSize) throw new Exception("node is not in the inner ring list");
            return Select(containers.ToArray(), epoch, (ulong)ind, (ulong)irSize);
        }

        private ContainerID[] Select(ContainerID[] containIds, ulong epoch, ulong index, ulong size)
        {
            if (index >= size) return null;
            ulong a = 0;
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
            List<ObjectID> sg = new List<ObjectID>();
            for (int i = 0; i < shuffled.Length; i++)
            {
                Dictionary<string, string> pairs = new Dictionary<string, string>();
                pairs.Add("cid", cid.ToBase58String());
                pairs.Add("address", shuffled[i].Info.Address);
                pairs.Add("try", i.ToString());
                pairs.Add("total_tries", shuffled.Length.ToString());
                string address;
                try
                {
                    address = Network.Address.IPAddrFromMultiaddr(shuffled[0].NetworkAddress);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Warning, string.Format("can't parse remote address,error {0}", e.Message));
                    continue;
                };
                API.Client.Client cli;
                try
                {
                    cli = ClientCache.Get(address);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Warning, string.Format("can't setup remote connection,error {0}", e.Message));
                    continue;
                };
                SearchFilters searchFilters = new SearchFilters();
                searchFilters.AddTypeFilter(MatchType.StringEqual, ObjectType.StorageGroup);
                try
                {
                    var source = new CancellationTokenSource();
                    source.CancelAfter(TimeSpan.FromMinutes(1));
                    List<ObjectID> result = cli.SearchObject(cid, searchFilters, context: source.Token).Result;
                    sg.AddRange(result);
                    break;
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Warning, string.Format("error in storage group search,error {0}", e.Message));
                    continue;
                }
            }
            return sg.ToArray();
        }
    }
}
