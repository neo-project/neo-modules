using System;
using Akka.Actor;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Invoker;
using static Neo.FileStorage.InnerRing.Events.MorphEvent;
using static Neo.FileStorage.InnerRing.Timer.BlockTimer;
using static Neo.FileStorage.InnerRing.Timer.TimerTickEvent;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class Helper
    {
        public static IActorRef NewEpochTimer(EpochTimerArgs args)
        {
            IActorRef epochTimer = args.context.ActorSystem.ActorOf(BlockTimer.Props(BlockTimer.StaticBlockMeter(Settings.Default.EpochDuration), () => { args.processor.HandleNewEpochTick(new NewEpochTickEvent()); }));
            epochTimer.Tell(new DeltaEvent()
            {
                mul = args.stopEstimationDMul,
                div = args.stopEstimationDDiv,
                h = () =>
                {
                    long epochN = (long)args.epoch.EpochCounter();
                    if (epochN == 0) return;
                    args.client.StopEstimation(epochN - 1);
                },
            });
            epochTimer.Tell(new DeltaEvent()
            {
                mul = args.collectBasicIncome.durationMul,
                div = args.collectBasicIncome.durationDiv,
                h = () =>
                {
                    ulong epochN = args.epoch.EpochCounter();
                    if (epochN == 0) return;
                    args.collectBasicIncome.handler(new BasicIncomeCollectEvent() { epoch = epochN - 1 });
                },
            });
            epochTimer.Tell(new DeltaEvent()
            {
                mul = args.distributeBasicIncome.durationMul,
                div = args.distributeBasicIncome.durationDiv,
                h = () =>
                {
                    ulong epochN = args.epoch.EpochCounter();
                    if (epochN == 0) return;
                    args.distributeBasicIncome.handler(new BasicIncomeDistributeEvent() { epoch = epochN - 1 });
                },
            });
            return epochTimer;
        }

        public static IActorRef NewEmissionTimer(EmitTimerArgs args)
        {
            return args.context.ActorSystem.ActorOf(BlockTimer.Props(BlockTimer.StaticBlockMeter(Settings.Default.AlphabetDuration), () => { args.processor.HandleGasEmission(new NewAlphabetEmitTickEvent()); }));
        }

        public class EmitTimerArgs
        {
            public NeoSystem context;
            public AlphabetContractProcessor processor;
            public uint epochDuration;
        }

        public class EpochTimerArgs
        {
            public NeoSystem context;
            public NetMapContractProcessor processor;
            public Client client;
            public IState epoch;
            public uint epochDuration;
            public uint stopEstimationDMul;
            public uint stopEstimationDDiv;
            public SubEpochEventHandler collectBasicIncome;
            public SubEpochEventHandler distributeBasicIncome;
        }

        public class SubEpochEventHandler
        {
            public Action<IContractEvent> handler;
            public uint durationMul;
            public uint durationDiv;
        }
    }
}
