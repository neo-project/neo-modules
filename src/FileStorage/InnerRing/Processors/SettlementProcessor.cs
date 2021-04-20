using Akka.Actor;
using Neo.FileStorage.API.Audit;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Morph.Event;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using static Neo.FileStorage.InnerRing.Events.MorphEvent;
using static Neo.FileStorage.InnerRing.Processors.SettlementProcessor;
using static Neo.FileStorage.InnerRing.Processors.SettlementProcessor.IncomeSettlementContext;
using static Neo.FileStorage.Morph.Invoker.MorphContractInvoker;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class SettlementProcessor : BaseProcessor
    {
        public override string Name => "SettlementProcessor";
        private AuditProcessor auditProc;
        private BasicIncomeInitializer basicIncome;
        private Dictionary<ulong, IncomeSettlementContext> incomeContexts;

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
        public void Handle(ulong epoch)
        {
            Utility.Log(Name, LogLevel.Info, "process audit settlements");
            auditProc.ProcessAuditSettlements(epoch);
            Utility.Log(Name, LogLevel.Info, "audit processing finished");
        }

        public void HandleIncomeCollectionEvent(IContractEvent morphEvent)
        {
            BasicIncomeCollectEvent basicIncomeCollectEvent = (BasicIncomeCollectEvent)morphEvent;
            var epoch = basicIncomeCollectEvent.epoch;
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore income collection event");
            }
            Utility.Log(Name, LogLevel.Info, string.Format("start basic income collection,epoch:{0}", epoch));
            if (incomeContexts.TryGetValue(epoch, out _)) {
                Utility.Log(Name, LogLevel.Error, string.Format("income context already exists,epoch:{0}", epoch));
                return;
            }
            IncomeSettlementContext incomeCtx = basicIncome.CreateContext(epoch);
            incomeContexts[epoch] = incomeCtx;
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => incomeCtx.Collect()) });
        }

        public void HandleIncomeDistributionEvent(IContractEvent morphEvent)
        {
            BasicIncomeDistributeEvent basicIncomeDistributeEvent = (BasicIncomeDistributeEvent)morphEvent;
            var epoch = basicIncomeDistributeEvent.epoch;
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore income distribution event");
            }
            Utility.Log(Name, LogLevel.Info, string.Format("start basic income distribution,epoch:{0}", epoch));
            var flag=incomeContexts.TryGetValue(epoch, out var incomeCtx);
            incomeContexts.Remove(epoch);
            if (!flag) {
                Utility.Log(Name, LogLevel.Info, string.Format("income context distribution does not exists,epoch:{0}", epoch));
                return;
            }
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => incomeCtx.Distribute()) });
        }

        public class IncomeSettlementContext
        {
            public BigInteger bigGB = new BigInteger(1 << 30);
            public BigInteger bigZero = new BigInteger(0);
            public BigInteger bigOne = new BigInteger(1);
            public ulong epoch;
            public RateFetcher rate;
            public EstimationFetcher estimations;
            public BalanceFetcher balances;
            public ContainerStorage container;
            public PlacementCalculator placement;
            public Exchanger exchange;
            public AccountStorage accounts;
            public OwnerID bankOwner;
            public NodeSizeTable distributeTable;

            public OwnerID BankOwnerID()
            {
                UInt160 u = new UInt160(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
                OwnerID o = OwnerID.FromBase58String(u.ToString());
                return o;
            }

            public void Collect()
            {
                var cachedRate = rate.BasicRate();
                var cnrEstimations = estimations.Estimations(epoch);
                var txTable = new TransferTable();
                foreach (var item in cnrEstimations) {
                    OwnerID owner = container.ContainerInfo(item.ContainerID).OwnerId;
                    NodeInfo[] cnrNodes = placement.ContainerNodes(epoch, item.ContainerID);
                    ulong avg = AvgEstimation(item);
                    BigInteger total=CalculateBasicSum(avg,cachedRate,cnrNodes.Length);
                    foreach (var node in cnrNodes)
                        distributeTable.Put(node.PublicKey(), avg);
                    txTable.Transfer(new TransferTable.TransferTx() { from=owner,to= BankOwnerID(),amount=total});
                }
            }

            public ulong AvgEstimation(Estimations e)
            {
                ulong avg = 0;
                if (e.AllEstimation.Count == 0) return avg;
                foreach (var estimation in e.AllEstimation)
                    avg += estimation.Size;
                return avg / (ulong)e.AllEstimation.Count;
            }

            public BigInteger CalculateBasicSum(ulong size,ulong rate,int ln) {
                BigInteger bigRate = rate;
                ulong total = size * (ulong)ln;
                BigInteger price = new BigInteger(total);
                price=BigInteger.Multiply(price, bigRate);
                price=BigInteger.Divide(price, bigGB);
                if (price.CompareTo(bigZero) == 0)
                    price=BigInteger.Add(price,bigOne);
                return price;
            }

            public void Distribute()
            {
                var txTable = new TransferTable();
                BigInteger bankBalance = balances.Balance(bankOwner);
                BigInteger total = distributeTable.Total();
                distributeTable.Iterate((byte[] key,BigInteger n)=> {
                    var nodeOwner=accounts.ResolveKey(new NodeInfoWrapper(key));
                    txTable.Transfer(new TransferTable.TransferTx() { from=bankOwner,to=nodeOwner,amount= NormalizedValue(n,total,bankBalance) });
                });
                TransferTable.TransferAssets(exchange, txTable);
            }

            public BigInteger NormalizedValue(BigInteger n,BigInteger total,BigInteger limit) {
                if (limit.CompareTo(bigZero) == 0) return 0;
                n=BigInteger.Multiply(n, limit);
                return BigInteger.Divide(n, total);
            }

            public class TransferTable {
                private Dictionary<string, Dictionary<string, TransferTx>> txs = new();

                public void Transfer(TransferTx tx) {
                    var from = tx.from.ToBase58String();
                    var to = tx.to.ToBase58String();
                    if (from == to) return;
                    if (!txs.TryGetValue(from, out var m)) {
                        if (!txs.TryGetValue(to, out m))
                        {
                            to = from;
                        }
                        else {
                            m = new Dictionary<string, TransferTx>();
                            txs[from] = m;
                        }
                    }
                    if (!m.TryGetValue(to, out var tgt)) {
                        m[to] = tx;
                        return;
                    }
                    tgt.amount += tx.amount;
                }

                public void Iterate(Action<TransferTx> f) {
                    foreach (var m in txs)
                        foreach (var tx in m.Value)
                            f(tx.Value);
                }

                public static void TransferAssets(Exchanger e, TransferTable t) {
                    t.Iterate((TransferTx tx) => {
                        var sign = tx.amount.Sign;
                        if (sign == 0) return;
                        if (sign < 0) {
                            OwnerID temp = tx.from;
                            tx.from = tx.to;
                            tx.to = temp;
                            tx.amount = BigInteger.Abs(tx.amount);
                        }
                        e.Transfer(tx.from, tx.to,tx.amount);
                    });
                }

                public class TransferTx{
                    public OwnerID from;
                    public OwnerID to;
                    public BigInteger amount;
                }
            }

            public class NodeSizeTable
            {
                private Dictionary<string, ulong> price=new();
                private ulong total;

                public void Put(byte[] id, ulong avg)
                {
                    price[System.Text.Encoding.UTF8.GetString(id)] += avg;
                    total += avg;
                }

                public BigInteger Total()
                {
                    return total;
                }

                public void Iterate(Action<byte[], BigInteger> f)
                {
                    foreach (var item in price)
                        f(System.Text.Encoding.UTF8.GetBytes(item.Key), item.Value);
                }
            }

            public interface EstimationFetcher
            {
                public Estimations[] Estimations(ulong epoch);
            }

            public interface RateFetcher
            {
                public ulong BasicRate();
            }

            public interface BalanceFetcher
            {
                public BigInteger Balance(OwnerID id);
            }
        }

        public class Calculator
        {
            public ResultStorage resultStorage;
            public ContainerStorage containerStorage;
            public PlacementCalculator placementCalculator;
            public SGStorage sGStorage;
            public AccountStorage accountStorage;
            public Exchanger exchanger;

            public void Calculate(ulong epoch)
            {
                Utility.Log("Calculator", LogLevel.Info, string.Format("current epoch,{0}", epoch));
                Utility.Log("Calculator", LogLevel.Info, "calculate audit settlements");
                Utility.Log("Calculator", LogLevel.Debug, "getting results for the previous epoch");
                List<DataAuditResult> auditResults = resultStorage.AuditResultsForEpoch(epoch - 1);
                if (auditResults.Count == 0) {
                    Utility.Log("Calculator",LogLevel.Debug, "no audit results in previous epoch");
                    return;
                }
                Utility.Log("Calculator", LogLevel.Debug, string.Format("processing audit results,number:{0}",auditResults.Count));
                var table = new TransferTable();
                foreach (var auditResult in auditResults) {
                    ProcessResult(new SingleResultCtx() {
                        auditResult=auditResult,
                        txTable=table
                    });
                }
                Utility.Log("Calculator", LogLevel.Debug, "processing transfers");
                TransferTable.TransferAssets(exchanger, table);
            }

            public void ProcessResult(SingleResultCtx ctx) {
                Utility.Log("Calculator", LogLevel.Debug, string.Format("cid:{0},audit epoch:{1}", ctx.cid.ToBase58String(),ctx.auditResult.AuditEpoch));
                Utility.Log("Calculator", LogLevel.Debug, "reading information about the container");
                ReadContainerInfo(ctx);
                Utility.Log("Calculator", LogLevel.Debug, "building placement");
                BuildPlacement(ctx);
                Utility.Log("Calculator", LogLevel.Debug, "collecting passed nodes");
                CollectPassNodes(ctx);
                Utility.Log("Calculator", LogLevel.Debug, "calculating sum of the sizes of all storage groups");
                SumSGSizes(ctx);
                Utility.Log("Calculator", LogLevel.Debug, "filling transfer table");
                FillTransferTable(ctx);
            }

            public bool ReadContainerInfo(SingleResultCtx ctx)
            {
                ctx.cnrInfo = containerStorage.ContainerInfo(ctx.auditResult.ContainerId);
                return true;
            }

            public bool BuildPlacement(SingleResultCtx ctx) {
                placementCalculator.ContainerNodes(ctx.eAudit,ctx.auditResult.ContainerId);
                bool empty = ctx.cnrNodes.Length == 0;
                return !empty;
            }

            public void CollectPassNodes(SingleResultCtx ctx) {
                ctx.passNodes = new Dictionary<string, NodeInfo>();
            }

            public bool SumSGSizes(SingleResultCtx ctx) {
                var passedSG = ctx.auditResult.PassSg;
                if (passedSG.Count == 0) {
                    Utility.Log("Calculator", LogLevel.Debug, "empty list of passed SG");
                    return false;
                }
                ulong sumPassSGSize = 0;
                API.Refs.Address address=new API.Refs.Address();
                address.ContainerId = ctx.cid;
                foreach (var sgID in ctx.auditResult.PassSg) {
                    address.ObjectId = sgID;
                    var sgInfo=sGStorage.SGInfo(address);
                    sumPassSGSize += sgInfo.Size();
                }
                if (sumPassSGSize == 0) {
                    Utility.Log("Calculator", LogLevel.Debug, "zero sum SG size");
                    return false;
                }
                ctx.sumSGSize = sumPassSGSize;
                return true;
            }

            public void FillTransferTable(SingleResultCtx ctx) {
                var cnrOwner = ctx.cnrInfo.OwnerId;
                foreach (var item in ctx.passNodes) {
                    var ownerID = accountStorage.ResolveKey(item.Value);
                    var price = item.Value.Price();
                    var fee = BigInteger.Multiply(price,ctx.sumSGSize);
                    fee = BigInteger.Divide(fee, BigInteger.One);
                    if (fee.CompareTo(BigInteger.Zero) == 0) fee = BigInteger.Add(fee,BigInteger.One);
                    ctx.txTable.Transfer(new TransferTable.TransferTx() { from=cnrOwner,to=ownerID,amount=fee});
                }
            }

            public class SingleResultCtx
            {
                public ulong eAudit;
                public DataAuditResult auditResult;
                public ContainerID cid;
                public TransferTable txTable;
                public Container cnrInfo;
                public NodeInfo[] cnrNodes;
                public Dictionary<string, NodeInfo> passNodes = new();
                public BigInteger sumSGSize;
            }
        }
    }

    public interface ResultStorage
    {
        public List<DataAuditResult> AuditResultsForEpoch(ulong epoch);
    }

    public interface NodeInfo {
        public BigInteger Price();
        public byte[] PublicKey();
    }
    public class NodeInfoWrapper : NodeInfo
    {
        private byte[] n;

        public NodeInfoWrapper(byte[] n)
        {
            this.n = n;
        }

        BigInteger NodeInfo.Price()
        {
            throw new Exception("should not be used");
        }

        byte[] NodeInfo.PublicKey()
        {
            return n;
        }
    }

    public interface ContainerStorage
    {
        public Container ContainerInfo(ContainerID containerID);
    }

    public interface PlacementCalculator
    {
        public NodeInfo[] ContainerNodes(ulong epoch, ContainerID containerID);
    }

    public interface AccountStorage
    {
        public OwnerID ResolveKey(NodeInfo nodeInfo);
    }

    public interface Exchanger
    {
        public void Transfer(OwnerID sender, OwnerID recipient, BigInteger amount);
    }

    public interface SGInfo
    {
        public ulong Size();
    }

    public interface SGStorage
    {
        public SGInfo SGInfo(API.Refs.Address address);
    }

    public interface BasicIncomeInitializer
    {
        public IncomeSettlementContext CreateContext(ulong epoch);
    }

    public interface AuditProcessor
    {
        public void ProcessAuditSettlements(ulong epoch);
    }
}
