using Neo.Plugins.FSStorage;

namespace Neo.Plugins.Innerring
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
