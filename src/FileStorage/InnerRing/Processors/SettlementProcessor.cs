using Akka.Actor;
using Neo.FileStorage.API.Audit;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Morph.Event;
using System.Numerics;
using System.Threading.Tasks;
using static Neo.FileStorage.InnerRing.Events.MorphEvent;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class SettlementProcessor : BaseProcessor, ResultStorage
    {
        public override string Name => "SettlementProcessor";

        public void HandleAuditEvent(IContractEvent morphEvent)
        {
            AuditEvent auditEvent = (AuditEvent)morphEvent;
            var epoch = auditEvent.epoch;
            Utility.Log(Name, LogLevel.Info, string.Format("new audit settlement event,epoch:{0}", epoch));
            if (epoch == 0)
            {
                Utility.Log(Name, LogLevel.Info, "ignore genesis epoch");
                return;
            }
            Utility.Log(Name, LogLevel.Info, "process audit settlements");
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => Handle(epoch)) });
            Utility.Log(Name, LogLevel.Info, "audit processing finished");
        }

        public void HandleIncomeCollectionEvent(IContractEvent morphEvent)
        {
            BasicIncomeCollectEvent basicIncomeCollectEvent = (BasicIncomeCollectEvent)morphEvent;
            var epoch = basicIncomeCollectEvent.epoch;
            if (!State.IsActive())
            {
                Utility.Log(Name, LogLevel.Info, "passive mode, ignore income collection event");
            }
            Utility.Log(Name, LogLevel.Info, string.Format("start basic income collection,epoch:{0}", epoch));

        }

        public void HandleIncomeDistributionEvent(IContractEvent morphEvent)
        {
            BasicIncomeDistributeEvent basicIncomeDistributeEvent = (BasicIncomeDistributeEvent)morphEvent;
            var epoch = basicIncomeDistributeEvent.epoch;
            if (!State.IsActive())
            {
                Utility.Log(Name, LogLevel.Info, "passive mode, ignore income distribution event");
            }
            Utility.Log(Name, LogLevel.Info, string.Format("start basic income distribution,epoch:{0}", epoch));

        }

        public void Handle(ulong epoch)
        {
            Utility.Log(Name, LogLevel.Info, "process audit settlements");
            ProcessAuditSettlements(epoch);
            Utility.Log(Name, LogLevel.Info, "audit processing finished");
        }

        public void Transfer(OwnerID sender, OwnerID recipient, int amount, byte[] details)
        {
            Utility.Log(Name, LogLevel.Info, string.Format("sender:{0},recipient:{1},amount (GASe-12):{2}", sender, recipient, amount));

            Utility.Log(Name, LogLevel.Info, "transfer transaction for audit was successfully sent");
        }

        public void Estimations(ulong epoch)
        {

        }

        public int Balance(OwnerID Id)
        {
            return 0;
        }

        public void ProcessAuditSettlements(ulong epoch)
        {

        }

        public IncomeSettlementContext CreateContext(ulong epoch)
        {
            return null;
        }

        public DataAuditResult AuditResultsForEpoch(ulong epoch)
        {
            throw new System.NotImplementedException();
        }

        public class IncomeSettlementContext
        {
            public ulong epoch;

            public void Collect()
            {

            }

            public OwnerID BankOwnerID()
            {
                UInt160 u = new UInt160(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
                OwnerID o = OwnerID.FromBase58String(u.ToString());
                return o;
            }
        }

        public class Calculator
        {
            public ResultStorage ResultStorage;

            public void Calculate(ulong epoch)
            {
                Utility.Log("Calculator", LogLevel.Info, string.Format("current epoch,{0}", epoch));
                Utility.Log("Calculator", LogLevel.Info, "calculate audit settlements");
                Utility.Log("Calculator", LogLevel.Debug, "getting results for the previous epoch");
            }
        }
    }

    public interface ResultStorage {
        public DataAuditResult AuditResultsForEpoch(ulong epoch);
    }

    public interface ContainerStorage
    {
        public Container ContainerInfo(ContainerID containerID);
    }

    public interface PlacementCalculator
    {
        public NodeInfo[] ContainerNodes(ulong epoch,ContainerID containerID);
    }

    public interface AccountStorage
    {
        public OwnerID ResolveKey(NodeInfo nodeInfo);
    }

    public interface Exchanger
    {
        public void Transfer(OwnerID sender, OwnerID recipient, BigInteger amount);
    }
}
