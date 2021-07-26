using System;
using Neo.FileStorage.Listen.Event;

namespace Neo.FileStorage.InnerRing.Events
{
    public class SyncEvent : ContractEvent
    {
        public ulong epoch;
    }

    public class DesignateEvent : ContractEvent
    {
        public byte role;
        public static DesignateEvent ParseDesignateEvent(VM.Types.Array eventParams)
        {
            var designateEvent = new DesignateEvent();
            if (eventParams.Count != 2) throw new FormatException();
            designateEvent.role = (byte)eventParams[0].GetInteger();
            return designateEvent;
        }
    }
}
