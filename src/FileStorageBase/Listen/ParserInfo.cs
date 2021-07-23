using System;
using Neo.FileStorage.Morph.Event;

namespace Neo.FileStorage.Morph.Listen
{
    public class ParserInfo
    {
        public ScriptHashWithType ScriptHashWithType;
        public Func<VM.Types.Array, ContractEvent> Parser;
    }
}
