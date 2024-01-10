// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.ApplicationLogs is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;

namespace Neo.Plugins.Store.States
{
    public class EngineLogState : ISerializable, IEquatable<EngineLogState>
    {
        public UInt160 ScriptHash { get; private set; } = UInt160.Zero;
        public string Message { get; private set; } = string.Empty;

        public static EngineLogState Create(UInt160 scriptHash, string message) =>
            new()
            {
                ScriptHash = scriptHash,
                Message = message,
            };

        #region ISerializable

        public virtual int Size =>
            ScriptHash.Size +
            Message.GetVarSize();

        public virtual void Deserialize(ref MemoryReader reader)
        {
            ScriptHash.Deserialize(ref reader);
            // It should be safe because it filled from a transaction's logs.
            Message = reader.ReadVarString();
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            ScriptHash.Serialize(writer);
            writer.WriteVarString(Message ?? string.Empty);
        }

        #endregion

        #region IEquatable

        public bool Equals(EngineLogState other) =>
            ScriptHash == other.ScriptHash &&
            Message == other.Message;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as EngineLogState);
        }

        public override int GetHashCode() =>
            HashCode.Combine(ScriptHash, Message);

        #endregion
    }
}
