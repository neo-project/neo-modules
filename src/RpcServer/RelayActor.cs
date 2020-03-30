using Akka.Actor;
using Neo.Network.P2P.Payloads;
using System.Collections.Generic;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins
{
    public class RelayActor : UntypedActor
    {
        private readonly NeoSystem neoSystem;
        private readonly Dictionary<UInt256, IActorRef> senders = new Dictionary<UInt256, IActorRef>();

        public RelayActor(NeoSystem neoSystem)
        {
            this.neoSystem = neoSystem;
            Context.System.EventStream.Subscribe(Self, typeof(RelayResult));
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case IInventory inventory:
                    this.senders.Add(inventory.Hash, Sender);
                    neoSystem.Blockchain.Tell(inventory);
                    break;
                case RelayResult reason:
                    UInt256 hash = reason.Inventory.Hash;
                    if (senders.ContainsKey(reason.Inventory.Hash))
                    {
                        senders[hash].Tell(reason);
                        senders.Remove(hash);
                    }
                    break;
            }
        }

        public static Props Props(NeoSystem neoSystem)
        {
            return Akka.Actor.Props.Create(() => new RelayActor(neoSystem));
        }
    }
}
