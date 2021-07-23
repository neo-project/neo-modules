using Neo.FileStorage.Listen.Event;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class NetmapCleanupTickEvent : ContractEvent
    {
        public ulong Epoch;
    }
}
