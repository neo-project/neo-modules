using Neo.FileStorage.API.Audit;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Invoker;
using System;
using System.Collections.Generic;
using System.Numerics;
using static Neo.FileStorage.InnerRing.Processors.SettlementProcessor;
using NodeInfo = Neo.FileStorage.InnerRing.Processors.NodeInfo;

namespace Neo.FileStorage.InnerRing
{
    public class SettlementDeps: ResultStorage, ContainerStorage, PlacementCalculator, AccountStorage, Exchanger, SGInfo, BasicIncomeInitializer, AuditProcessor
    {
        private Client auditClient;

        public List<DataAuditResult> AuditResultsForEpoch(ulong epoch)
        {
            List<byte[]> idList = MorphContractInvoker.InvokeListAuditResultsByEpoch(auditClient, (long)epoch);
            var res = new List<DataAuditResult>();
            foreach (var id in idList) {
                DataAuditResult dataAuditResult = DataAuditResult.Parser.ParseFrom(id);
                res.Add(dataAuditResult);
            }
            return res;
        }

        public ContainerInfo ContainerInfo(ContainerID containerID)
        {
            throw new NotImplementedException();
        }
        public NodeInfo[] ContainerNodes(ulong epoch, ContainerID containerID)
        {
            throw new NotImplementedException();
        }

        public void BuildContainer() { }

        public OwnerID ResolveKey(NodeInfo nodeInfo)
        {
            throw new NotImplementedException();
        }

        void Exchanger.Transfer(OwnerID sender, OwnerID recipient, BigInteger amount)
        {
            throw new NotImplementedException();
        }

        public ulong Size()
        {
            throw new NotImplementedException();
        }

        public virtual void Transfer(OwnerID sender, OwnerID recipient, int amount, byte[] details)
        {
            Utility.Log(Name, LogLevel.Info, string.Format("sender:{0},recipient:{1},amount (GASe-12):{2}", sender, recipient, amount));

            Utility.Log(Name, LogLevel.Info, "transfer transaction for audit was successfully sent");
        }

        public IncomeSettlementContext CreateContext(ulong epoch)
        {
            throw new NotImplementedException();
        }

        public void ProcessAuditSettlements(ulong epoch)
        {
            //C//
        }
    }

    public class AuditSettlementDeps : SettlementDeps
    {
        public override void Transfer(OwnerID sender, OwnerID recipient, int amount, byte[] details)
        {
            base.Transfer(sender, recipient, amount, details);
        }
    };
    public class BasicIncomeSettlementDeps : SettlementDeps { };
}
