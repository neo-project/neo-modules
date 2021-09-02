using Akka.Actor;
using Neo.FileStorage.API.Netmap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FSAddress = Neo.FileStorage.API.Refs.Address;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Replicate
{
    public class Replicator : UntypedActor
    {
        public class Args
        {
            public static readonly TimeSpan DefaultPutTimeout = TimeSpan.FromSeconds(5);
            public TimeSpan PutTimeout { get; init; } = DefaultPutTimeout;
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
            catch
            {
                return;
            }
            RemotePutPrm prm = new()
            {
                Object = obj,
            };
            for (int i = 0; i < task.Nodes.Count && 0 < task.Quantity; i++)
            {
                prm.Addresses = task.Nodes[i].NetworkAddresses.Select(p => Network.Address.FromString(p)).ToList();
                using CancellationTokenSource srouce = new(args.PutTimeout);
                args.RemoteSender.PutObject(prm, srouce.Token);
                task.Quantity--;
            }
        }

        public static Props Props(Args c)
        {
            return Akka.Actor.Props.Create(() => new Replicator(c));
        }
    }
}
