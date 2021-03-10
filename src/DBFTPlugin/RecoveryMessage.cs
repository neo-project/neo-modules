using Neo.IO;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Consensus
{
    public partial class RecoveryMessage : ConsensusMessage
    {
        public Dictionary<int, ChangeViewPayloadCompact> ChangeViewMessages;
        public PrepareRequest PrepareRequestMessage;
        /// The PreparationHash in case the PrepareRequest hasn't been received yet.
        /// This can be null if the PrepareRequest information is present, since it can be derived in that case.
        public UInt256 PreparationHash;
        public Dictionary<int, PreparationPayloadCompact> PreparationMessages;
        public Dictionary<int, CommitPayloadCompact> CommitMessages;

        public override int Size => base.Size
            + /* ChangeViewMessages */ ChangeViewMessages?.Values.GetVarSize() ?? 0
            + /* PrepareRequestMessage */ 1 + PrepareRequestMessage?.Size ?? 0
            + /* PreparationHash */ PreparationHash?.Size ?? 0
            + /* PreparationMessages */ PreparationMessages?.Values.GetVarSize() ?? 0
            + /* CommitMessages */ CommitMessages?.Values.GetVarSize() ?? 0;

        public RecoveryMessage(byte validatorsCount) : base(validatorsCount, ConsensusMessageType.RecoveryMessage)
        {
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            ChangeViewMessages = new Dictionary<int, ChangeViewPayloadCompact>();
            ulong count = reader.ReadVarInt(validatorsCount);
            for (ulong i = 0; i < count; i++)
            {
                ChangeViewPayloadCompact payload = new(validatorsCount);
                payload.Deserialize(reader);
                ChangeViewMessages.Add(payload.ValidatorIndex, payload);
            }
            if (reader.ReadBoolean())
            {
                PrepareRequestMessage = new PrepareRequest(validatorsCount);
                PrepareRequestMessage.Deserialize(reader);
            }
            else
            {
                int preparationHashSize = UInt256.Zero.Size;
                if (preparationHashSize == (int)reader.ReadVarInt((ulong)preparationHashSize))
                    PreparationHash = new UInt256(reader.ReadFixedBytes(preparationHashSize));
            }

            PreparationMessages = new Dictionary<int, PreparationPayloadCompact>();
            count = reader.ReadVarInt(validatorsCount);
            for (ulong i = 0; i < count; i++)
            {
                PreparationPayloadCompact payload = new(validatorsCount);
                payload.Deserialize(reader);
                PreparationMessages.Add(payload.ValidatorIndex, payload);
            }

            CommitMessages = new Dictionary<int, CommitPayloadCompact>();
            count = reader.ReadVarInt(validatorsCount);
            for (ulong i = 0; i < count; i++)
            {
                CommitPayloadCompact payload = new(validatorsCount);
                payload.Deserialize(reader);
                CommitMessages.Add(payload.ValidatorIndex, payload);
            }
        }

        internal ExtensiblePayload[] GetChangeViewPayloads(ConsensusContext context)
        {
            return ChangeViewMessages.Values.Select(p => context.CreatePayload(new ChangeView(validatorsCount)
            {
                BlockIndex = BlockIndex,
                ValidatorIndex = p.ValidatorIndex,
                ViewNumber = p.OriginalViewNumber,
                Timestamp = p.Timestamp
            }, p.InvocationScript)).ToArray();
        }

        internal ExtensiblePayload[] GetCommitPayloadsFromRecoveryMessage(ConsensusContext context)
        {
            return CommitMessages.Values.Select(p => context.CreatePayload(new Commit(validatorsCount)
            {
                BlockIndex = BlockIndex,
                ValidatorIndex = p.ValidatorIndex,
                ViewNumber = p.ViewNumber,
                Signature = p.Signature
            }, p.InvocationScript)).ToArray();
        }

        internal ExtensiblePayload GetPrepareRequestPayload(ConsensusContext context)
        {
            if (PrepareRequestMessage == null) return null;
            if (!PreparationMessages.TryGetValue(context.Block.PrimaryIndex, out PreparationPayloadCompact compact))
                return null;
            return context.CreatePayload(PrepareRequestMessage, compact.InvocationScript);
        }

        internal ExtensiblePayload[] GetPrepareResponsePayloads(ConsensusContext context)
        {
            UInt256 preparationHash = PreparationHash ?? context.PreparationPayloads[context.Block.PrimaryIndex]?.Hash;
            if (preparationHash is null) return Array.Empty<ExtensiblePayload>();
            return PreparationMessages.Values.Where(p => p.ValidatorIndex != context.Block.PrimaryIndex).Select(p => context.CreatePayload(new PrepareResponse(validatorsCount)
            {
                BlockIndex = BlockIndex,
                ValidatorIndex = p.ValidatorIndex,
                ViewNumber = ViewNumber,
                PreparationHash = preparationHash
            }, p.InvocationScript)).ToArray();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(ChangeViewMessages.Values.ToArray());
            bool hasPrepareRequestMessage = PrepareRequestMessage != null;
            writer.Write(hasPrepareRequestMessage);
            if (hasPrepareRequestMessage)
                writer.Write(PrepareRequestMessage);
            else
            {
                if (PreparationHash == null)
                    writer.WriteVarInt(0);
                else
                    writer.WriteVarBytes(PreparationHash.ToArray());
            }

            writer.Write(PreparationMessages.Values.ToArray());
            writer.Write(CommitMessages.Values.ToArray());
        }
    }
}
