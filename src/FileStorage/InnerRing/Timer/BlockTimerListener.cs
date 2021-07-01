using System;
using Akka.Actor;
using static Neo.FileStorage.InnerRing.Timer.BlockTimer;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class BlockTimerListener : UntypedActor
    {
        public class DeltaEvent
        {
            public uint mul; public uint div; public Action h; public Action<DeltaCfg>[] opts;
        }
        public class ResetEvent { };
        public class TickEvent { };

        private BlockTimer timer;

        public BlockTimerListener(Func<uint> dur, Action h, uint pmul = 1, uint pdiv = 1)
        {
            timer = new BlockTimer(dur,h,pmul,pdiv);
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case DeltaEvent deltaEvent:
                    timer.Delta(deltaEvent.mul, deltaEvent.div, deltaEvent.h, deltaEvent.opts);
                    break;
                case ResetEvent _:
                    timer.Reset();
                    break;
                case TickEvent _:
                    timer.Tick();
                    break;
                default:
                    break;
            }
        }

        public static Props Props(Func<uint> dur, Action h, uint mul = 1, uint div = 1)
        {
            return Akka.Actor.Props.Create(() => new BlockTimerListener(dur, h, mul, div));
        }
    }
}
