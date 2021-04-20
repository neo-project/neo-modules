using Google.Protobuf;
using Neo.FileStorage.API.Audit;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Core.Container;
using Neo.FileStorage.Core.Netmap;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Invoker;
using System;
using System.Collections.Generic;
using System.Numerics;
using static Neo.FileStorage.InnerRing.Processors.SettlementProcessor;
using static Neo.FileStorage.InnerRing.Processors.SettlementProcessor.IncomeSettlementContext;
using NodeInfo = Neo.FileStorage.InnerRing.Processors.NodeInfo;

namespace Neo.FileStorage.InnerRing
{
    public class SettlementDeps: ResultStorage, ContainerStorage, PlacementCalculator, AccountStorage, Exchanger, SGInfo,AuditProcessor, SGStorage
    {
        public Client client;
        public INetmapSource nmSrc;
        public IContainerSource cnrSrc;
        public RpcClientCache clientCache;

        public List<DataAuditResult> AuditResultsForEpoch(ulong epoch)
        {
            List<byte[]> idList = MorphContractInvoker.InvokeListAuditResultsByEpoch(client, (long)epoch);
            var res = new List<DataAuditResult>();
            foreach (var id in idList) {
                DataAuditResult dataAuditResult = DataAuditResult.Parser.ParseFrom(id);
                res.Add(dataAuditResult);
            }
            return res;
        }

        public Container ContainerInfo(ContainerID cid)
        {
            return cnrSrc.Get(cid);

        }
        public NodeInfo[] ContainerNodes(ulong epoch, ContainerID cid)
        {
            BuildContainer(epoch, cid, out List<List<Node>> cn, out NetMap netMap);
            List<Node> ns = cn.Flatten();
            List<NodeInfo> res = new List<NodeInfo>();
            foreach (var node in ns)
                res.Add(new NodeInfoWrapper(node));
            return res.ToArray();
        }

        public void BuildContainer(ulong epoch,ContainerID cid,out List<List<Node>> containerNodes,out NetMap netMap) {
            if (epoch > 0)
                netMap = nmSrc.GetNetMapByEpoch(epoch);
            else
                netMap = nmSrc.GetLatestNetworkMap();
            Container cnr = cnrSrc.Get(cid);
            containerNodes = netMap.GetContainerNodes(cnr.PlacementPolicy,cid.Value.ToByteArray());
        }



        public ulong Size()
        {
            throw new NotImplementedException();
        }

        public OwnerID ResolveKey(NodeInfo nodeInfo)
        {
            return OwnerID.Frombytes(nodeInfo.PublicKey());
        }


        void Exchanger.Transfer(OwnerID sender, OwnerID recipient, BigInteger amount)
        {
            Utility.Log("SettlementDeps", LogLevel.Info, string.Format("sender:{0},recipient:{1},amount (GASe-12):{2}", sender, recipient, amount));
            //notary
            Utility.Log("SettlementDeps", LogLevel.Info, "transfer transaction for audit was successfully sent");
        }

        public void ProcessAuditSettlements(ulong epoch)
        {
            //C//
        }

        public SGInfo SGInfo(Address address)
        {
            BuildContainer(0, address.ContainerId, out var cn, out var nm);
            //clientCache.GetStorageGroup();
            return null;
        }
    }

    public class NodeInfoWrapper : NodeInfo
    {
        private Node ni;

        public NodeInfoWrapper(Node ni)
        {
            this.ni = ni;
        }

        public BigInteger Price()
        {
            return ni.Price;
        }

        public byte[] PublicKey()
        {
            return ni.PublicKey;
        }
    }

    public class AuditSettlementDeps : SettlementDeps
    {
    };
    public class BasicIncomeSettlementDeps : SettlementDeps, EstimationFetcher,BalanceFetcher,RateFetcher, BasicIncomeInitializer
    {
        public BigInteger Balance(OwnerID id)
        {
            return client.InvokeBalanceOf(id.ToByteArray());
        }

        public ulong BasicRate()
        {
            throw new NotImplementedException();
        }

        public IncomeSettlementContext CreateContext(ulong epoch)
        {
            return new IncomeSettlementContext() {
                epoch=epoch,
                rate= this,
                estimations=this,
                balances = this,
                container = this,
                placement = this,
                exchange = this,
                accounts = this,

            };
        }

        public MorphContractInvoker.Estimations[] Estimations(ulong epoch)
        {
            //cnrClient;
            return null;
        }
    };
}
