using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Neo.FileStorage.API.Audit;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;
using Neo.FileStorage.Invoker.Morph;
using Neo.IO;

namespace Neo.FileStorage.InnerRing.Processors
{
    public abstract class SettlementDeps
    {
        public MorphInvoker Invoker;
        public RpcClientCache ClientCache;

        public List<DataAuditResult> AuditResultsForEpoch(ulong epoch)
        {
            List<byte[]> idList = Invoker.ListAuditResultsByEpoch((long)epoch);
            var res = new List<DataAuditResult>();
            foreach (var id in idList)
            {
                DataAuditResult dataAuditResult = Invoker.GetAuditResult(id);
                res.Add(dataAuditResult);
            }
            return res;
        }

        public Container ContainerInfo(ContainerID cid)
        {
            return Invoker.GetContainer(cid).Container;
        }

        public void BuildContainer(ulong epoch, ContainerID cid, out List<List<Node>> containerNodes, out NetMap netMap)
        {
            if (epoch > 0)
                netMap = Invoker.GetNetMapByEpoch(epoch);
            else
                netMap = Invoker.GetNetMapByDiff(0);
            Container cnr = Invoker.GetContainer(cid).Container;
            containerNodes = netMap.GetContainerNodes(cnr.PlacementPolicy, cid.Value.ToByteArray());
        }

        public Node[] ContainerNodes(ulong epoch, ContainerID cid)
        {
            BuildContainer(epoch, cid, out List<List<Node>> cn, out NetMap _);
            return cn.Flatten().ToArray();
        }

        public StorageGroup SGInfo(Address address)
        {
            BuildContainer(0, address.ContainerId, out var cn, out var nm);
            return ClientCache.GetStorageGroup(new CancellationToken(), address, nm, cn);
        }

        public void Transfer(OwnerID sender, OwnerID recipient, long amount, byte[] details)
        {
            Utility.Log("SettlementDeps", LogLevel.Info, $"sender:{sender.ToAddress()},recipient:{recipient.ToAddress()},amount (GASe-12):{amount},details:{Utility.StrictUTF8.GetString(details)}");
            Invoker.TransferX(sender.ToScriptHash().ToArray(), recipient.ToScriptHash().ToArray(), amount, details);
        }

        public abstract void Transfer(OwnerID sender, OwnerID recipient, long amount);
    }
}
