using System;
using Neo.FileStorage.Listen.Event;

namespace Neo.FileStorage.Listen
{
    public class ParserInfo
    {
        public ScriptHashWithType ScriptHashWithType;
        public Func<VM.Types.Array, ContractEvent> Parser;
    }
}
