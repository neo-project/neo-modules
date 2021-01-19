namespace Neo.Plugins.FSStorage
{
    public class ScriptHashWithType
    {
        public UInt160 ScriptHashValue;
        public string Type;

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            ScriptHashWithType oth = (ScriptHashWithType)obj;
            if (this == oth) return true;
            if (oth.Type is null && oth.ScriptHashValue is null) return false;
            if (this.Type is null && oth.Type is null) return this.ScriptHashValue.Equals(oth.ScriptHashValue);
            if (this.ScriptHashValue is null && oth.ScriptHashValue is null) return this.Type.Equals(oth.Type);
            return this.Type.Equals(oth.Type) && this.ScriptHashValue.Equals(oth.ScriptHashValue);
        }

        public override int GetHashCode()
        {
            return ScriptHashValue.GetHashCode() + Type.GetHashCode();
        }
    }
}
