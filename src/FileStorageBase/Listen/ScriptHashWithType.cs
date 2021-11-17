using System;

namespace Neo.FileStorage.Listen
{
    public class ScriptHashWithType
    {
        public UInt160 ScriptHashValue;
        public string Type;

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj is not ScriptHashWithType other) return false;
            return other.ScriptHashValue == ScriptHashValue && other.Type == Type;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ScriptHashValue.GetHashCode(), Type.GetHashCode());
        }
    }
}
