using Neo.FileStorage.InnerRing.Events;
using Neo.FileStorage.Listen;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class Helper
    {
        public static BlockTimer NewEpochTimer(EpochTimerArgs args)
        {
            BlockTimer epochTimer = new(BlockTimer.StaticBlockMeter(Settings.Default.EpochDuration), () => { args.Processor.HandleNewEpochTick(); });
            epochTimer.Delta(
                args.StopEstimationDMul,
                args.StopEstimationDDiv,
                () =>
                {
                    long epochN = (long)args.EpochState.EpochCounter();
                    if (epochN == 0) return;
                    args.MorphInvoker.StopEstimation(epochN - 1);
                });
            epochTimer.Delta(
                args.CollectBasicIncome.DurationMul,
                args.CollectBasicIncome.DurationDiv,
                () =>
                {
                    ulong epochN = args.EpochState.EpochCounter();
                    if (epochN == 0) return;
                    args.CollectBasicIncome.Handler(new BasicIncomeCollectEvent() { Epoch = epochN - 1 });
                });
            epochTimer.Delta(
                args.DistributeBasicIncome.DurationMul,
                args.DistributeBasicIncome.DurationDiv,
                () =>
                {
                    ulong epochN = args.EpochState.EpochCounter();
                    if (epochN == 0) return;
                    args.DistributeBasicIncome.Handler(new BasicIncomeDistributeEvent() { Epoch = epochN - 1 });
                });
            return epochTimer;
        }

        public static BlockTimer NewEmissionTimer(EmitTimerArgs args)
        {
            return new BlockTimer(BlockTimer.StaticBlockMeter(Settings.Default.AlphabetDuration), () => { args.Processor.HandleGasEmission(); });
        }
    }
}
