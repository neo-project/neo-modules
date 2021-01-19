using Akka.Actor;
using Neo.Plugins.FSStorage.innerring.processors;
using System;
using static Neo.Plugins.FSStorage.innerring.timers.EpochTickEvent;

namespace Neo.Plugins.FSStorage.innerring.timers
{
    public class Timers : UntypedActor
    {
        public const string EpochTimer = "EpochTimer";
        public const string AlphabetTimer = "AlphabetTimer";
        public class Timer { public IContractEvent contractEvent; }
        public class Start { };
        public class Stop { };
        public class BindTimersEvent { public IProcessor processor; };

        private LocalTimer epochTimer = new LocalTimer() { Duration = Settings.Default.EpochDuration };
        private LocalTimer alphabetTimer = new LocalTimer() { Duration = Settings.Default.AlphabetDuration };
        private bool started = false;

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case BindTimersEvent bindTimersEvent:
                    BindProcessor(bindTimersEvent.processor);
                    break;
                case Timer timer:
                    OnTimer(timer.contractEvent);
                    break;
                case Start _:
                    OnStart();
                    break;
                case Stop _:
                    OnStop();
                    break;
                default:
                    break;
            }
        }

        private void OnStart()
        {
            if (!started)
            {
                started = !started;
                OnTimer(new NewAlphabetEmitTickEvent());
                OnTimer(new NewEpochTickEvent());
            }
        }

        private void OnStop()
        {
            if (started)
            {
                started = !started;
                epochTimer.Timer_token.CancelIfNotNull();
                alphabetTimer.Timer_token.CancelIfNotNull();
            }
        }

        private void OnTimer(IContractEvent contractEvent)
        {
            if (started)
            {
                LocalTimer timer = null;
                if (contractEvent is NewAlphabetEmitTickEvent)
                {
                    timer = alphabetTimer;
                }
                else if (contractEvent is NewEpochTickEvent)
                {
                    timer = epochTimer;
                }
                TimeSpan duration = TimeSpan.FromMilliseconds(timer.Duration);
                timer.Timer_token.CancelIfNotNull();
                if (timer.Handler != null) timer.Handler(contractEvent);
                timer.Timer_token = Context.System.Scheduler.ScheduleTellOnceCancelable(duration, Self, new Timer { contractEvent = contractEvent }, ActorRefs.NoSender);
            }
        }

        public void BindProcessor(IProcessor processor)
        {
            HandlerInfo[] handlers = processor.TimersHandlers();
            foreach (HandlerInfo handler in handlers)
            {
                RegisterHandler(handler);
            }
        }

        public void RegisterHandler(HandlerInfo p)
        {
            if (p.Handler is null) throw new Exception("ir/timers: can't register nil handler");
            switch (p.ScriptHashWithType.Type)
            {
                case EpochTimer:
                    epochTimer.Handler = p.Handler;
                    break;
                case AlphabetTimer:
                    alphabetTimer.Handler = p.Handler;
                    break;
                default:
                    throw new Exception("ir/timers: unknown handler type");
            }
        }

        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new Timers());
        }

        public class LocalTimer
        {
            private long duration;
            private ICancelable timer_token;
            private Action<IContractEvent> handler;

            public long Duration { get => duration; set => duration = value; }
            public ICancelable Timer_token { get => timer_token; set => timer_token = value; }
            public Action<IContractEvent> Handler { get => handler; set => handler = value; }
        }
    }
}
