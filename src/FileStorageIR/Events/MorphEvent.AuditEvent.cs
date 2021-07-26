using Neo.FileStorage.Listen.Event;

namespace Neo.FileStorage.InnerRing.Events
{
    public class AuditStartEvent : ContractEvent
    {
        public ulong epoch;
    }

    public class BasicIncomeCollectEvent : AuditStartEvent { }

    public class BasicIncomeDistributeEvent : AuditStartEvent { }
}
