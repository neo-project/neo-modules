using System;
using Neo.FileStorage.API.Reputation;

namespace Neo.FileStorage.Morph.Event
{
    public partial class MorphEvent
    {
        public class ReputationPutEvent : ContractEvent
        {
            public ulong Epoch;
            public byte[] PeerID;
            public GlobalTrust Trust;

            public static ReputationPutEvent ParseReputationPutEvent(VM.Types.Array eventParams)
            {
                var reputationPutEvent = new ReputationPutEvent();
                if (eventParams.Count != 3) throw new FormatException();
                reputationPutEvent.Epoch = (ulong)eventParams[0].GetInteger();
                reputationPutEvent.PeerID = eventParams[1].GetSpan().ToArray();
                if (reputationPutEvent.PeerID.Length != 33) throw new FormatException($"invalid peer ID size, expected={33}, actual={reputationPutEvent.PeerID.Length}");
                reputationPutEvent.Trust = GlobalTrust.Parser.ParseFrom(eventParams[2].GetSpan().ToArray());
                return reputationPutEvent;
            }
        }
    }
}
