using System;
using Neo.Cryptography.ECC;
using Neo.IO;

namespace Neo.FileStorage.Morph.Event
{
    partial class MorphEvent
    {
        public class NewEpochEvent : ContractEvent
        {
            public ulong EpochNumber;

            public static NewEpochEvent ParseNewEpochEvent(VM.Types.Array eventParams)
            {
                var newEpochEvent = new NewEpochEvent();
                if (eventParams.Count != 1) throw new Exception();
                newEpochEvent.EpochNumber = (ulong)eventParams[0].GetInteger();
                return newEpochEvent;
            }
        }

        public class AddPeerEvent : ContractEvent
        {
            public byte[] Node;

            public static AddPeerEvent ParseAddPeerEvent(VM.Types.Array eventParams)
            {
                var addPeerEvent = new AddPeerEvent();
                if (eventParams.Count != 1) throw new Exception();
                addPeerEvent.Node = eventParams[0].GetSpan().ToArray();
                return addPeerEvent;
            }
        }

        public class UpdatePeerEvent : ContractEvent
        {
            public ECPoint PublicKey;
            public uint Status;

            public static UpdatePeerEvent ParseUpdatePeerEvent(VM.Types.Array eventParams)
            {
                var updatePeerEvent = new UpdatePeerEvent();
                if (eventParams.Count != 2) throw new Exception();
                updatePeerEvent.Status = (uint)eventParams[0].GetInteger();
                updatePeerEvent.PublicKey = eventParams[1].GetSpan().ToArray().AsSerializable<ECPoint>();
                return updatePeerEvent;
            }
        }
    }
}
