using Neo.FileStorage.Morph.Event;

namespace Neo.FileStorage.InnerRing.Events
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
