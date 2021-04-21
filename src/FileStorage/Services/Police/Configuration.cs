using Akka.Actor;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Object.Head;
using Neo.FileStorage.Services.ObjectManager.Placement;
using System;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Services.Police
{
    public class Configuration
    {
        public int ExpandRate { get; init; }
        public int WorkScope { get; set; }
        public TimeSpan HeadTimeout { get; init; }
        public Network.Address LocalAddress { get; init; }
        public Client MorphClient { get; init; }
        public IActorRef ReplicatorRef { get; init; }
        public StorageEngine LocalStorage { get; init; }
        public IPlacementBuilder PlacementBuilder { get; init; }
        public RemoteHeader RemoteHeader { get; init; }
        public Action<FSAddress> RedundantCopyCallback { get; init; }
    }
}
