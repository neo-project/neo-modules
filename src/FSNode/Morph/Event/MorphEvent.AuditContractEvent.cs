using Neo.IO;
using System;

namespace Neo.Plugins.FSStorage
{
    public partial class MorphEvent
    {
        public class StartEvent : IContractEvent
        {
            public ulong epoch;
            public void ContractEvent() { }
        }
    }
}
