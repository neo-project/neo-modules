using Akka.Actor;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Invoker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Neo.FileStorage.InnerRing.Timer.EpochTickEvent;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class Helper
    {
        public static IActorRef NewEpochTimer(EpochTimerArgs args)
        {
            return args.context.ActorSystem.ActorOf(BlockTimer.Props(BlockTimer.StaticBlockMeter(Settings.Default.EpochDuration), () => { args.processor.HandleNewEpochTick(new NewEpochTickEvent()); }));
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
