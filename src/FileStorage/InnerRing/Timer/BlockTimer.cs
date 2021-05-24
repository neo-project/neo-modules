using Akka.Actor;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.InnerRing.Timer
{
    public class BlockTimer : UntypedActor
    {
        public class DeltaEvent
        {
            public uint mul; public uint div; public Action h; public Action<DeltaCfg>[] opts;
        }
        public class ResetEvent { };
        public class ResetWithBaseIntervalEvent { public uint d; }
        public class TickEvent { };

        public class DeltaCfg { public bool pulse; }
        private bool rolledBack;
        private Func<uint> dur;
        private uint baseDur;
        private uint mul;
        private uint div;
        private uint cur;
        private uint tgt;
        private Action h;
        private List<IActorRef> ps;
        private DeltaCfg deltaCfg;

        public BlockTimer(Func<uint> dur, Action h, uint pmul = 1, uint pdiv = 1)
        {
            this.dur = dur;
            this.mul = pmul;
            this.div = pdiv;
            this.h = h;
            this.ps = new List<IActorRef>();
            this.deltaCfg = new DeltaCfg() { pulse = true };
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case DeltaEvent deltaEvent:
                    OnDelta(deltaEvent.mul, deltaEvent.div, deltaEvent.h, deltaEvent.opts);
                    break;
                case ResetEvent _:
                    OnReset();
                    break;
                case ResetWithBaseIntervalEvent resetWithBaseIntervalEvent:
                    OnResetWithBaseInterval(resetWithBaseIntervalEvent.d);
                    break;
                case TickEvent _:
                    OnTick();
                    break;
                default:
                    break;
            }
        }

        private void OnDelta(uint mul, uint div, Action h, Action<DeltaCfg>[] opts)
        {
            var c = new DeltaCfg() { pulse = false };
            if(opts is not null) foreach (var opt in opts) opt(c);
            ps.Add(Context.ActorOf(BlockTimer.Props(null, h, mul, div)));
        }

        private void OnReset()
        {
            var d = dur();
            ResetWithBaseInterval(d);
            foreach (var process in ps)
            {
                process.Tell(new ResetWithBaseIntervalEvent() { d = d });
            }

        }
        private void OnResetWithBaseInterval(uint d)
        {
            ResetWithBaseInterval(d);
        }

        private void OnTick()
        {
            Tick();
        }

        private void ResetWithBaseInterval(uint d)
        {
            rolledBack = false;
            baseDur = d;
            Reset();
        }
        private void Reset()
        {
            var mul = this.mul;
            var div = this.div;
            if (!deltaCfg.pulse && rolledBack && mul < div) mul = div = 1;
            var delta = mul * baseDur / div;
            if (delta == 0) delta = 1;
            tgt = delta;
            cur = 0;
        }

        private void Tick()
        {
            cur++;
            if (cur == tgt)
            {
                h();
                cur = 0;
                rolledBack = true;
                Reset();
            }
            foreach (var process in ps) process.Tell(new TickEvent());
        }
        public static Props Props(Func<uint> dur, Action h, uint mul = 1, uint div = 1)
        {
            return Akka.Actor.Props.Create(() => new BlockTimer(dur, h, mul, div));
        }

        public static Func<uint> StaticBlockMeter(uint d)
        {
            return () => { return d; };
        }
    }
}
