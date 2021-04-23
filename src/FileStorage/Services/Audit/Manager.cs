using Akka.Actor;
using Neo.FileStorage.Services.Audit.Auditor;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neo.FileStorage.Services.Audit
{
    public class Manager : UntypedActor
    {
        public class ResetMessage { }
        private class Start { }

        public const int DefaultCapacity = 100;
        private readonly int taskQueueCapacity = DefaultCapacity;
        private readonly IContainerCommunicator communicator;
        private readonly ulong maxPDPInterval;//MillisecondsTimeout
        private readonly Queue<AuditTask> taskQueue;
        private Task runningTask;

        public Manager(int capacity, IContainerCommunicator container_communicator, ulong max_pdp_interval)
        {
            taskQueueCapacity = capacity;
            taskQueue = new Queue<AuditTask>(taskQueueCapacity);
            communicator = container_communicator;
            maxPDPInterval = max_pdp_interval;
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case AuditTask task:
                    PushTask(task);
                    break;
                case Start _:
                    HandleTask();
                    break;
                case ResetMessage _:
                    Sender.Tell(Reset());
                    break;
            }
        }

        private void PushTask(AuditTask task)
        {
            if (taskQueueCapacity <= taskQueue.Count)
                return;
            taskQueue.Enqueue(task);
            if (runningTask is null || runningTask.Status != TaskStatus.Running)
                Self.Tell(new Start());
        }

        private void HandleTask()
        {
            if (runningTask != null && runningTask.Status == TaskStatus.Running)
                return;
            if (!taskQueue.TryDequeue(out AuditTask task))
                return;
            var context = new Context
            {
                ContainerCommunacator = communicator,
                AuditTask = task,
                MaxPDPInterval = maxPDPInterval,
            };
            runningTask = Task.Run(() =>
            {
                context.Execute();
                Self.Tell(new Start());
            });
        }

        private int Reset()
        {
            var count = taskQueue.Count;
            taskQueue.Clear();
            return count;
        }

        public static Props Props(int capacity, IContainerCommunicator container_communicator, ulong max_pdp_interval)
        {
            return Akka.Actor.Props.Create(() => new Manager(capacity, container_communicator, max_pdp_interval));
        }
    }
}
