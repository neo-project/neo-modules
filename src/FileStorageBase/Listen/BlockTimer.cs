using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Listen
{
    public class BlockTimer
    {
        public class DeltaCfg { public bool pulse; }
        private bool _rolledBack;
        private readonly Func<uint> _dur;
        private uint _baseDur;
        private readonly uint _mul;
        private readonly uint _div;
        private uint _cur;
        private uint _tgt;
        private readonly Action _h;
        private readonly List<BlockTimer> _ps;
        private readonly DeltaCfg _deltaCfg;

        public BlockTimer(Func<uint> dur, Action h, uint pmul = 1, uint pdiv = 1)
        {
            _dur = dur;
            _mul = pmul;
            _div = pdiv;
            _h = h;
            _ps = new List<BlockTimer>();
            _deltaCfg = new DeltaCfg() { pulse = true };
        }

        public void Delta(uint mul, uint div, Action h, Action<DeltaCfg>[] opts = null)
        {
            var c = new DeltaCfg() { pulse = false };
            if (opts is not null) foreach (var opt in opts) opt(c);
            _ps.Add(new BlockTimer(null, h, mul, div));
        }

        public void Reset()
        {
            var d = _dur();
            ResetWithBaseInterval(d);
            foreach (var process in _ps)
            {
                process.ResetWithBaseInterval(d);
            }
        }

        public void ResetWithBaseInterval(uint d)
        {
            _rolledBack = false;
            _baseDur = d;
            OnReset();
        }

        private void OnReset()
        {
            var mul = _mul;
            var div = _div;
            if (!_deltaCfg.pulse && _rolledBack && mul < div) mul = div = 1;
            var delta = mul * _baseDur / div;
            if (delta == 0) delta = 1;
            _tgt = delta;
            _cur = 0;
        }

        public void Tick()
        {
            _cur++;
            if (_cur == _tgt)
            {
                _h();
                _cur = 0;
                _rolledBack = true;
                OnReset();
            }
            foreach (var process in _ps) process.Tick();
        }

        public static Func<uint> StaticBlockMeter(uint d)
        {
            return () => { return d; };
        }
    }
}
