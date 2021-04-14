using Neo.Plugins.FSStorage;

namespace Neo.Plugins.Innerring
{
    public partial class MorphEvent
    {
        public class SyncEvent : IContractEvent
        {
            public ulong epoch;
            public void ContractEvent() { }
        }
    }
}
