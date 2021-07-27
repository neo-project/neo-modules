using Neo.FileStorage.InnerRing.Processors;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class EmitTimerArgs
    {
        public AlphabetContractProcessor Processor;
        public uint EpochDuration;
    }
}
