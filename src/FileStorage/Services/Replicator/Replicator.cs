using Akka.Actor;
using Neo.FileStorage.API.Netmap;
using V2Address = Neo.FileStorage.API.Refs.Address;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Network.Cache;
using Neo.FileStorage.Services.Object.Util;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Services.Replicator
{
    public class Replicator : UntypedActor
    {
        public class Task
        {
            public uint Quantity;
            public V2Address Address;
            public List<Node> Nodes;
        }

        public int TaskCapacity;
        public TimeSpan PutTimeout;
        public StorageEngine LocalStorage;
        public KeyStorage KeyStorage;
        public ClientCache ClientCache;

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Task task:
                    OnTask(task);
                    break;
            }
        }

        private void OnTask(Task task)
        {
            var obj = LocalStorage.Get(task.Address);
            if (obj is null)
                throw new InvalidOperationException(nameof(Replicator) + "could not get object from local storage");
            for (int i = 0; i < task.Nodes.Count && 0 < task.Quantity; i++)
            {
                var net_address = task.Nodes[i].NetworkAddress;
                var node = Network.Address.AddressFromString(net_address);
                //Timeout context
                // var remote_sender = new RemoteStore
                // {
                //     KeyStorage = KeyStorage,
                //     ClientCache = ClientCache,
                //     Node = node,
                // };
                // remote_sender.Put(obj);
                task.Quantity--;
            }
        }
    }
}
