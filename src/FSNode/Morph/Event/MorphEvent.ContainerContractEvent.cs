using Neo.Cryptography.ECC;
using Neo.IO;
using System;

namespace Neo.Plugins.FSStorage
{
    public partial class MorphEvent
    {
        public class ContainerDeleteEvent : IContractEvent
        {
            public byte[] ContainerID;
            public byte[] Signature;

            public void ContractEvent() { }
        }

        public class ContainerPutEvent : IContractEvent
        {
            public byte[] RawContainer;
            public byte[] Signature;
            public ECPoint PublicKey;
            public void ContractEvent() { }
        }

        public static ContainerDeleteEvent ParseContainerDeleteEvent(VM.Types.Array eventParams)
        {
            var containerDeleteEvent = new ContainerDeleteEvent();
            if (eventParams.Count != 2) throw new Exception();
            containerDeleteEvent.ContainerID = eventParams[0].GetSpan().ToArray();
            containerDeleteEvent.Signature = eventParams[1].GetSpan().ToArray();
            return containerDeleteEvent;
        }

        public static ContainerPutEvent ParseContainerPutEvent(VM.Types.Array eventParams)
        {
            var containerPutEvent = new ContainerPutEvent();
            if (eventParams.Count != 3) throw new Exception();
            containerPutEvent.RawContainer = eventParams[0].GetSpan().ToArray();
            containerPutEvent.Signature = eventParams[1].GetSpan().ToArray();
            containerPutEvent.PublicKey = eventParams[2].GetSpan().ToArray().AsSerializable<ECPoint>();
            return containerPutEvent;
        }
    }
}
