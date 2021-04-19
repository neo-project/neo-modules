using Akka.Actor;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Invoker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class Helper
    {
        public static IActorRef NewEpochTimer(EpochTimerArgs args)
        {
            system.ActorSystem.ActorOf(BlockTimer.Props(BlockTimer.StaticBlockMeter(Settings.Default.EpochDuration), () => { netMapContractProcessor.HandleNewEpochTick(new NewEpochTickEvent()); }));
            return null;
        }

        public static IActorRef NewEmissionTimer(EmitTimerArgs args)
        {
            var emissionTimer = system.ActorSystem.ActorOf(BlockTimer.Props(BlockTimer.StaticBlockMeter(Settings.Default.AlphabetDuration), () => { alphabetContractProcessor.HandleGasEmission(new NewAlphabetEmitTickEvent()); }));
            system.ActorSystem.ActorOf(BlockTimer.Props(BlockTimer.StaticBlockMeter(Settings.Default.EpochDuration), () => { netMapContractProcessor.HandleNewEpochTick(new NewEpochTickEvent()); }));
            return null;
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
