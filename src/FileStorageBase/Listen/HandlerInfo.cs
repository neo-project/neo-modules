using System;
using Neo.FileStorage.Listen.Event;

namespace Neo.FileStorage.Listen
{
    public class HandlerInfo
    {
        public ScriptHashWithType ScriptHashWithType;
        public Action<ContractEvent> Handler;
    }
}
