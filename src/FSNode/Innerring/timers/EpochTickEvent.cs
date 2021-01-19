namespace Neo.Plugins.FSStorage.innerring.timers
{
    public class EpochTickEvent
    {
        public class NewEpochTickEvent : IContractEvent
        {
            public void ContractEvent() { }
        }

        public class NewAlphabetEmitTickEvent : IContractEvent
        {
            public void ContractEvent() { }
        }

        public class NetmapCleanupTickEvent : IContractEvent
        {
            public ulong Epoch;

            public void ContractEvent() { }
        }
    }
}
