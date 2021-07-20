using Neo.FileStorage.Morph.Event;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class TimerTickEvent
    {
        public class NewEpochTickEvent : ContractEvent
        {
        }

        public class NewAlphabetEmitTickEvent : ContractEvent
        {
        }

        public class NetmapCleanupTickEvent : ContractEvent
        {
            public ulong Epoch;
        }
    }
}
