using Neo.FileStorage.Morph.Event;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class TimerTickEvent
    {
        public class NetmapCleanupTickEvent : ContractEvent
        {
            public ulong Epoch;
        }
    }
}
