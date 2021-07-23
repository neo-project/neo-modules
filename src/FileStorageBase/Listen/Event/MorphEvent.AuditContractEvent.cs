namespace Neo.FileStorage.Morph.Event
{
    public partial class MorphEvent
    {
        public class StartEvent : ContractEvent
        {
            public ulong epoch;
        }
    }
}
