using Google.Protobuf;
using Neo.FileStorage.API.Audit;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;
using Neo.FileStorage.Core.Container;
using Neo.FileStorage.Core.Netmap;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Invoker;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using static Neo.FileStorage.Morph.Invoker.MorphContractInvoker;
using NodeInfo = Neo.FileStorage.InnerRing.Processors.NodeInfo;

namespace Neo.FileStorage.InnerRing
{
    public abstract class SettlementDeps
    {
        public Client client;
        public INetmapSource nmSrc;
        public IContainerSource cnrSrc;
        public RpcClientCache clientCache;

        public List<DataAuditResult> AuditResultsForEpoch(ulong epoch)
        {
            List<byte[]> idList = MorphContractInvoker.InvokeListAuditResultsByEpoch(client, (long)epoch);
            var res = new List<DataAuditResult>();
            foreach (var id in idList)
            {
                DataAuditResult dataAuditResult = DataAuditResult.Parser.ParseFrom(id);
                res.Add(dataAuditResult);
            }
            return res;
        }

        public Container ContainerInfo(ContainerID cid)
        {
            return cnrSrc.Get(cid);

        }

        public void BuildContainer(ulong epoch, ContainerID cid, out List<List<Node>> containerNodes, out NetMap netMap)
        {
            if (epoch > 0)
                netMap = nmSrc.GetNetMapByEpoch(epoch);
            else
                netMap = nmSrc.GetLatestNetworkMap();
            Container cnr = cnrSrc.Get(cid);
            containerNodes = netMap.GetContainerNodes(cnr.PlacementPolicy, cid.Value.ToByteArray());
        }

        public NodeInfo[] ContainerNodes(ulong epoch, ContainerID cid)
        {
            BuildContainer(epoch, cid, out List<List<Node>> cn, out NetMap netMap);
            List<Node> ns = cn.Flatten();
            List<NodeInfo> res = new List<NodeInfo>();
            foreach (var node in ns)
                res.Add(new NormalNodeInfoWrapper(node));
            return res.ToArray();
        }

        public StorageGroup SGInfo(Address address)
        {
            BuildContainer(0, address.ContainerId, out var cn, out var nm);
            return clientCache.GetStorageGroup(new CancellationToken(), address, nm, cn);
        }

        public OwnerID ResolveKey(NodeInfo nodeInfo)
        {
            return OwnerID.FromByteArray(nodeInfo.PublicKey());
        }

        public void transfer(OwnerID sender, OwnerID recipient, BigInteger amount, byte[] details)
        {
            Utility.Log("SettlementDeps", LogLevel.Info, string.Format("sender:{0},recipient:{1},amount (GASe-12):{2}", sender, recipient, amount));
            //notary
            Utility.Log("SettlementDeps", LogLevel.Info, "transfer transaction for audit was successfully sent");
        }

        public abstract void Transfer(OwnerID sender, OwnerID recipient, BigInteger amount);
    }

    public class AuditSettlementDeps : SettlementDeps
    {
        public override void Transfer(OwnerID sender, OwnerID recipient, BigInteger amount)
        {
            transfer(sender, recipient, amount, System.Text.Encoding.UTF8.GetBytes("settlement-audit"));
        }
    };
    public class BasicIncomeSettlementDeps : SettlementDeps
    {
        public ulong BasicRate => Settings.Default.BasicIncomeRate;
        public override void Transfer(OwnerID sender, OwnerID recipient, BigInteger amount)
        {
            transfer(sender, recipient, amount, System.Text.Encoding.UTF8.GetBytes("settlement-basic-income"));
        }

        public BigInteger Balance(OwnerID id)
        {
            return client.InvokeBalanceOf(id.ToByteArray());
        }

        public Estimations[] Estimations(ulong epoch)
        {
            List<byte[]> estimationIDs = client.InvokeListSizes(epoch);
            List<Estimations> result = new List<Estimations>();
            foreach (var estimationID in estimationIDs)
            {
                try
                {
                    Estimations estimation = client.InvokeGetContainerSize(ContainerID.Parser.ParseFrom(estimationID));
                    result.Add(estimation);
                }
                catch (Exception e)
                {
                    Utility.Log("BasicIncomeSettlementDeps", LogLevel.Warning, string.Format("can't get used space estimation,estimation_id:{0},error:{1}", estimationID.ToHexString(), e.Message));
                }
            }
            return result.ToArray();
        }
    };
}
