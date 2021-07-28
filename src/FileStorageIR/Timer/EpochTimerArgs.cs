using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Invoker.Morph;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class EpochTimerArgs
    {
        public NetMapContractProcessor Processor;
        public MorphInvoker MorphInvoker;
        public IState EpochState;
        public uint EpochDuration;
        public uint StopEstimationDMul;
        public uint StopEstimationDDiv;
        public SubEpochEventHandler CollectBasicIncome;
        public SubEpochEventHandler DistributeBasicIncome;
    }
}
