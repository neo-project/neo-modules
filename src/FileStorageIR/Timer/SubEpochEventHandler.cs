using System;
using Neo.FileStorage.Listen.Event;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class SubEpochEventHandler
    {
        public Action<ContractEvent> Handler;
        public uint DurationMul;
        public uint DurationDiv;
    }
}
