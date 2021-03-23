using Akka.Actor;
using Google.Protobuf;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Invoker;
using Neo.Plugins.Innerring.Processors;
using Neo.FileStorage.API.Client.ObjectParams;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.FileStorage.Morph.Event.MorphEvent;
using static Neo.Plugins.util.WorkerPool;
using Neo.FileStorage.Services.Audit;
using static Neo.FileStorage.Services.Audit.Manager;
using System.Threading;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class AuditContractProcessor : IProcessor
    {
        private string name = "AuditContractProcessor";

        public IClient Client;
        public IActorRef WorkPool;
        public IActorRef TaskManager;
        public IIndexer Indexer;
        public INeoFSClientCache ClientCache;
        public ulong SearchTimeout => Settings.Default.SearchTimeout;
        public Action prevAuditCanceler = new Action(() => { });
        public IReporter reporter;

        public string Name { get => name; set => name = value; }

        public HandlerInfo[] ListenerHandlers()
        {
            return new HandlerInfo[] { };
        }

        public ParserInfo[] ListenerParsers()
        {
            return new ParserInfo[] { };
        }

        public HandlerInfo[] TimersHandlers()
        {
            return new HandlerInfo[] { };
        }

        public void HandleNewAuditRound(IContractEvent morphEvent)
        {
            StartEvent startEvent = (StartEvent)morphEvent;
            Neo.Utility.Log(Name, LogLevel.Info, string.Format("new round of audit,epoch:{0}", startEvent.epoch));
            WorkPool.Tell(new NewTask() { process = Name, task = new System.Threading.Tasks.Task(() => ProcessStartAudit(startEvent.epoch)) });
        }

        public void ProcessStartAudit(ulong epoch)
        {
            Neo.Utility.Log(Name, LogLevel.Info, string.Format("epoch:{0}", epoch));
            prevAuditCanceler();
            int skipped = (int)TaskManager.Ask(new ResetMessage()).Result;
            if (skipped > 0)
            {
                Neo.Utility.Log(Name, LogLevel.Info, string.Format("some tasks from previous epoch are skipped,amount{0}", skipped));
            }
            ContainerID[] containers;
            try
            {
                containers = SelectContainersToAudit(epoch);
            }
            catch (Exception e)
            {
                Neo.Utility.Log(Name, LogLevel.Error, string.Format("container selection failure,error {0}", e.Message));
                return;
            }
            Neo.Utility.Log(Name, LogLevel.Info, string.Format("select containers for audit,amount {0}", containers.Length));
            NetMap nm;
            try
            {
                List<NodeInfo> infos = new List<NodeInfo>();
                byte[][] rawPeers = MorphContractInvoker.InvokeSnapshot(Client, 0);
                foreach (var item in rawPeers)
                {
                    var nodeInfo = NodeInfo.Parser.ParseFrom(item);
                    infos.Add(nodeInfo);
                }
                List<Node> nodes = infos.Select((p, i) => new Node(i, p)).ToList();
                nm = new NetMap(nodes.ToArray());
            }
            catch (Exception e)
            {
                Neo.Utility.Log(Name, LogLevel.Error, string.Format("can't fetch network map,error {0}", e.Message));
                return;
            }
            for (int i = 0; i < containers.Length; i++)
            {
                Container cnr;
                try
                {
                    var rawContainer = MorphContractInvoker.InvokeGetContainer(Client, containers[i].ToByteArray());
                    cnr = Container.Parser.ParseFrom(rawContainer);
                }
                catch (Exception e)
                {
                    Neo.Utility.Log(Name, LogLevel.Error, string.Format("can't get container info, ignore,cid:{0},error:{1}", containers[i], e.Message));
                    continue;
                }
                List<List<Node>> nodes;
                try
                {
                    nodes = nm.GetContainerNodes(cnr.PlacementPolicy, null);
                }
                catch (Exception e)
                {
                    Neo.Utility.Log(Name, LogLevel.Error, string.Format("can't build placement for container, ignore,cid:{0},error:{1}", containers[i], e.Message));
                    continue;
                }
                var n = nodes.Flatten().ToArray();
                n = n.OrderBy(c => Guid.NewGuid()).ToArray();
                var storageGroups = FindStorageGroups(containers[i], n);
                Neo.Utility.Log(Name, LogLevel.Info, string.Format("select storage groups for audit,cid:{0},amount:{1}", containers[i], storageGroups.Length));

                var source = new System.Threading.CancellationTokenSource();
                AuditTask auditTask = new AuditTask()
                {
                    Reporter = new EpochAuditReporter()
                    {
                        epoch = epoch,
                        reporter = reporter
                    },
                    Context = source.Token,
                    CID = containers[i],
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
                    Neo.Utility.Log(Name, LogLevel.Info, string.Format("could not push audit task,errot:{0}", e.Message));
                }
            }
        }

        private ContainerID[] SelectContainersToAudit(ulong epoch)
        {
            byte[][] rawcontainers;
            try
            {
                rawcontainers = MorphContractInvoker.InvokeGetContainerList(Client, new byte[0]);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("can't get list of containers to start audit,error {0}", e.Message));
            }
            List<ContainerID> containers = rawcontainers.Select(p => ContainerID.FromSha256Bytes(p)).ToList();
            Neo.Utility.Log(Name, LogLevel.Info, string.Format("container listing finished,total amount {0}", containers.Count));
            containers.Sort((x, y) => x.ToBase58String().CompareTo(y.ToBase58String()));
            var ind = Indexer.Index();
            var irSize = Indexer.InnerRingSize();
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
                    address = Neo.FileStorage.Network.Address.IPAddrFromMultiaddr(shuffled[0].NetworkAddress);
                }
                catch (Exception e)
                {
                    Neo.Utility.Log(Name, LogLevel.Warning, string.Format("can't parse remote address,error {0}", e.Message));
                    continue;
                };
                Neo.FileStorage.API.Client.Client cli;
                try
                {
                    cli = ClientCache.Get(address);
                }
                catch (Exception e)
                {
                    Neo.Utility.Log(Name, LogLevel.Warning, string.Format("can't setup remote connection,error {0}", e.Message));
                    continue;
                };
                SearchFilters searchFilters = new SearchFilters();
                searchFilters.AddTypeFilter(MatchType.StringEqual, ObjectType.StorageGroup);
                try
                {
                    var source = new CancellationTokenSource();
                    source.CancelAfter(TimeSpan.FromMinutes(1));
                    List<ObjectID> result = cli.SearchObject(new SearchObjectParams { ContainerID = cid, Filters = searchFilters }, context: source.Token).Result;
                    sg.AddRange(result);
                    break;
                }
                catch (Exception e)
                {
                    Neo.Utility.Log(Name, LogLevel.Warning, string.Format("error in storage group search,error {0}", e.Message));
                    continue;
                }
            }
            return sg.ToArray();
        }
    }
}
