using Neo.FileStorage.Morph.Event;

namespace Neo.FileStorage.InnerRing.Events
{
    public partial class MorphEvent
    {
        public class AuditEvent : IContractEvent
        {
            public ulong epoch;
            public void ContractEvent() { }
        }

        public class BasicIncomeCollectEvent : AuditEvent { }
        public class BasicIncomeDistributeEvent : AuditEvent { }
    }
}
