using Neo.FileStorage.Morph.Event;

namespace Neo.FileStorage.InnerRing.Events
{
    public partial class MorphEvent
    {
        public class AuditStartEvent : ContractEvent
        {
            public ulong epoch;
        }

        public class BasicIncomeCollectEvent : AuditStartEvent { }

        public class BasicIncomeDistributeEvent : AuditStartEvent { }
    }
}
