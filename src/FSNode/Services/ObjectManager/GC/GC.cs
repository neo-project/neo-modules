using Akka.Actor;
using V2Address = NeoFS.API.v2.Refs.Address;
using System;
using System.Collections.Generic;

namespace Neo.FSNode.Services.ObjectManager.GC
{
    public class GC : UntypedActor
    {
        public IRemover Remover;
        public int QueueCapacity;
        public TimeSpan WorkingInterval;
        public TimeSpan SleepingInterval;

        private class Timer { }
        private bool working = false;
        private Queue<V2Address> Queue;

        public GC()
        {
            Queue = new Queue<V2Address>(QueueCapacity);
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case V2Address[] addresses:
                    DeleteObjects(addresses);
                    break;
                case Timer _:
                    OnTimer();
                    break;
            }
        }

        private void OnTimer()
        {
            working = !working;
            ResetTimer();
            if (working)
                while (Queue.TryDequeue(out V2Address address))
                {
                    Remover.Delete(address);
                }
        }

        private void ResetTimer()
        {
            Context.System.Scheduler.ScheduleTellOnceCancelable(working ? SleepingInterval : WorkingInterval, Self, new Timer
            { }, ActorRefs.NoSender);
        }

        private void DeleteObjects(V2Address[] addresses)
        {
            foreach (var address in addresses)
                if (working)
                    Remover.Delete(address);
                else if (Queue.Count < QueueCapacity)
                    Queue.Enqueue(address);
        }
    }
}
