using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neo.FileStorage.Utils
{
    /// <summary>
    /// WorkerPool is a thread pool that uses Akka's message mechanism internally.
    /// Multiple instances can be created, and the task scheduling is independent.
    /// Through its internal timer, it will periodically select a certain count of
    /// tasks from the task list for execution.
    /// [free=capacity-running]
    /// </summary>
    public class WorkerPool : UntypedActor
    {
        private string name;
        private int capacity;
        private int running;

        public class NewTask { public string process; public Task task; };
        public class CompleteTask { };

        public WorkerPool(string name, int capacity)
        {
            this.name = name;
            this.capacity = capacity;
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case NewTask newTask:
                    OnNewTask(newTask);
                    break;
                case CompleteTask completeTask:
                    OnCompleteTask();
                    break;
                default:
                    break;
            }
        }

        private void OnNewTask(NewTask newTask)
        {
            var actor = Self;
            int free = capacity - running;
            if (free == 0)
            {
                Neo.Utility.Log(newTask.process, LogLevel.Warning, string.Format("worker pool drained,capacity:{0}", capacity.ToString()));
            }
            else
            {
                newTask.task.ContinueWith(t => { actor.Tell(new CompleteTask()); });
                newTask.task.Start();
                running++;
            }
        }

        private void OnCompleteTask()
        {
            running--;
        }

        public static Props Props(string name, int capacity)
        {
            return Akka.Actor.Props.Create(() => new WorkerPool(name, capacity));
        }
    }
}
