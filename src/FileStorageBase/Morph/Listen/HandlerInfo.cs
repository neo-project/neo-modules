using System;
using Neo.FileStorage.Morph.Event;

namespace Neo.FileStorage.Morph.Listen
{
    public class HandlerInfo
    {
        public ScriptHashWithType ScriptHashWithType;
        public Action<IContractEvent> Handler;
    }
}
