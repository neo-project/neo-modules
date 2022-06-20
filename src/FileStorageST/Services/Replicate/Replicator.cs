using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Threading;
using Neo.FileStorage.API.Netmap;
using FSAddress = Neo.FileStorage.API.Refs.Address;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Replicate
{
    public class Replicator : UntypedActor
    {
        public class Args
        {
            public static readonly int DefaultPutTimeout = 5;
            public TimeSpan PutTimeout { get; init; } = TimeSpan.FromSeconds(DefaultPutTimeout);
            public IRemoteSender RemoteSender { get; init; }
            public ILocalObjectSource LocalStorage { get; init; }
        }

        public class Task
        {
            public uint Quantity;
            public FSAddress Address;
            public List<Node> Nodes;
        }

        private readonly Args args;

        public Replicator(Args c)
        {
            args = c;
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Task task:
                    HandleTask(task);
                    break;
            }
        }

        private void HandleTask(Task task)
        {
            FSObject obj;
            try
            {
                obj = args.LocalStorage.Get(task.Address);
            }
            catch (Exception e)
            {
                Utility.Log(nameof(Replicator), LogLevel.Debug, $"could not get object from local storages, object={task.Address.String()}, error={e.Message}");
                return;
            }
            RemotePutPrm prm = new()
            {
                Object = obj,
            };
            for (int i = 0; i < task.Nodes.Count && 0 < task.Quantity; i++)
            {
                prm.Node = task.Nodes[i].Info;
                using CancellationTokenSource cancellationSrouce = new(args.PutTimeout);
                try
                {
                    args.RemoteSender.PutObject(prm, cancellationSrouce.Token);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(Replicator), LogLevel.Debug, $"could not replicate object, node={string.Join("|", task.Nodes[i].Addresses)}, object={task.Address.String()}, error={e.Message}");
                    continue;
                }
                Utility.Log(nameof(Replicator), LogLevel.Debug, $"object successfully replicated, node={string.Join("|", task.Nodes[i].Addresses)}, object={task.Address.String()}");
                task.Quantity--;
            }
            Utility.Log(nameof(Replicator), LogLevel.Debug, $"object replicate finish, unfinished={task.Quantity}");
        }

        public static Props Props(Args c)
        {
            return Akka.Actor.Props.Create(() => new Replicator(c));
        }
    }
}
