using System;

namespace Neo.Plugins.FSStorage
{
    public class ParserInfo
    {
        public ScriptHashWithType ScriptHashWithType;
        public Func<VM.Types.Array, IContractEvent> Parser;
    }
}
