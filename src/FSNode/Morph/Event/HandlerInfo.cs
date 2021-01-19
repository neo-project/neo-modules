using System;

namespace Neo.Plugins.FSStorage
{
    public class HandlerInfo
    {
        public ScriptHashWithType ScriptHashWithType;
        public Action<IContractEvent> Handler;
    }
}
