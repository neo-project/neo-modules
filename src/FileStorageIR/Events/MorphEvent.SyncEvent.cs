using System;
using Neo.FileStorage.Listen.Event;

namespace Neo.FileStorage.InnerRing.Events
{
    public class SyncEvent : ContractEvent
    {
        public ulong Epoch;
    }

    public class DesignateEvent : ContractEvent
    {
        public byte Role;
        public static DesignateEvent ParseDesignateEvent(VM.Types.Array eventParams)
        {
            var designateEvent = new DesignateEvent();
            if (eventParams.Count != 2) throw new FormatException();
            designateEvent.Role = (byte)eventParams[0].GetInteger();
            return designateEvent;
        }
    }
}
