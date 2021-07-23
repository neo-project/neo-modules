using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Akka.Actor;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.InnerRing.Events;
using Neo.FileStorage.Listen.Event;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class SettlementProcessor : BaseProcessor
    {
        public override string Name => "SettlementProcessor";
        private readonly Dictionary<ulong, IncomeSettlementContext> incomeContexts = new();
        public BasicIncomeSettlementDeps basicIncome;
        public Calculator auditProc;

        public void Handle(ulong epoch)
        {
            Utility.Log(Name, LogLevel.Info, "process audit settlements");
            auditProc.Calculate(epoch);
            Utility.Log(Name, LogLevel.Info, "audit processing finished");
        }

        public void HandleAuditEvent(ContractEvent morphEvent)
        {
            AuditStartEvent auditEvent = (AuditStartEvent)morphEvent;
            var epoch = auditEvent.epoch;
            Utility.Log(Name, LogLevel.Info, $"new audit settlement event, epoch={epoch}");
            if (epoch == 0)
            {
                Utility.Log(Name, LogLevel.Info, "ignore genesis epoch");
                return;
            }
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => Handle(epoch)) });
            Utility.Log(Name, LogLevel.Info, "AuditEvent handling successfully scheduled");
        }

        public void HandleIncomeCollectionEvent(ContractEvent morphEvent)
        {
            BasicIncomeCollectEvent basicIncomeCollectEvent = (BasicIncomeCollectEvent)morphEvent;
            var epoch = basicIncomeCollectEvent.epoch;
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore income collection event");
                return;
            }
            Utility.Log(Name, LogLevel.Info, $"start basic income collection, epoch={epoch}");
            if (incomeContexts.TryGetValue(epoch, out _))
            {
                Utility.Log(Name, LogLevel.Error, $"income context already exists, epoch={epoch}");
                return;
            }
            IncomeSettlementContext incomeCtx = new() { settlementDeps = basicIncome, epoch = epoch };
            incomeCtx.bankOwner = incomeCtx.BankOwnerID();
            incomeContexts[epoch] = incomeCtx;
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => incomeCtx.Collect()) });
        }

        public void HandleIncomeDistributionEvent(ContractEvent morphEvent)
        {
            BasicIncomeDistributeEvent basicIncomeDistributeEvent = (BasicIncomeDistributeEvent)morphEvent;
            var epoch = basicIncomeDistributeEvent.epoch;
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore income distribution event");
                return;
            }
            Utility.Log(Name, LogLevel.Info, $"start basic income distribution, epoch={epoch}");
            var flag = incomeContexts.TryGetValue(epoch, out var incomeCtx);
            incomeContexts.Remove(epoch);
            if (!flag)
            {
                Utility.Log(Name, LogLevel.Info, $"income context distribution does not exists, epoch={epoch}");
                return;
            }
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => incomeCtx.Distribute()) });
        }
    }
}
