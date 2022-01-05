using System;

namespace Neo.FileStorage.Listen.Event.Morph
{
    public class ContainerDeleteEvent : ContractEvent
    {
        public byte[] ContainerID;
        public byte[] Signature;
        public byte[] Token;

        public static ContainerDeleteEvent ParseContainerDeleteEvent(VM.Types.Array eventParams)
        {
            var containerDeleteEvent = new ContainerDeleteEvent();
            if (eventParams.Count != 3) throw new FormatException();
            containerDeleteEvent.ContainerID = eventParams[0].GetSpan().ToArray();
            containerDeleteEvent.Signature = eventParams[1].GetSpan().ToArray();
            containerDeleteEvent.Token = eventParams[2] is VM.Types.Null ? null : eventParams[2].GetSpan().ToArray();
            return containerDeleteEvent;
        }
    }

    public class ContainerPutEvent : ContractEvent
    {
        public byte[] RawContainer;
        public byte[] Signature;
        public byte[] PublicKey;
        public byte[] Token;

        public static ContainerPutEvent ParseContainerPutEvent(VM.Types.Array eventParams)
        {
            var containerPutEvent = new ContainerPutEvent();
            if (eventParams.Count != 4) throw new FormatException();
            containerPutEvent.RawContainer = eventParams[0].GetSpan().ToArray();
            containerPutEvent.Signature = eventParams[1].GetSpan().ToArray();
            containerPutEvent.PublicKey = eventParams[2].GetSpan().ToArray();
            containerPutEvent.Token = eventParams[3] is VM.Types.Null ? null : eventParams[3].GetSpan().ToArray();
            return containerPutEvent;
        }
    }

    public class ContainerSetEACLEvent : ContractEvent
    {
        public byte[] Table;
        public byte[] Signature;
        public byte[] PublicKey;
        public byte[] Token;

        public static ContainerSetEACLEvent ParseContainerSetEACLEvent(VM.Types.Array eventParams)
        {
            var containerSetEACLEvent = new ContainerSetEACLEvent();
            if (eventParams.Count != 4) throw new FormatException();
            containerSetEACLEvent.Table = eventParams[0].GetSpan().ToArray();
            containerSetEACLEvent.Signature = eventParams[1].GetSpan().ToArray();
            containerSetEACLEvent.PublicKey = eventParams[2].GetSpan().ToArray();
            containerSetEACLEvent.Token = eventParams[3].GetSpan().ToArray();
            return containerSetEACLEvent;
        }
    }

    public class StartEstimationEvent : ContractEvent
    {
        public ulong Epoch;

        public static StartEstimationEvent ParseStartEstimationEvent(VM.Types.Array eventParams)
        {
            var startEstimationEvent = new StartEstimationEvent();
            if (eventParams.Count != 1) throw new FormatException();
            startEstimationEvent.Epoch = (ulong)eventParams[0].GetInteger();
            return startEstimationEvent;
        }
    }

    public class StopEstimationEvent : ContractEvent
    {
        public ulong Epoch;

        public static StopEstimationEvent ParseStopEstimationEvent(VM.Types.Array eventParams)
        {
            var stopEstimationEvent = new StopEstimationEvent();
            if (eventParams.Count != 1) throw new FormatException();
            stopEstimationEvent.Epoch = (ulong)eventParams[0].GetInteger();
            return stopEstimationEvent;
        }
    }
}
