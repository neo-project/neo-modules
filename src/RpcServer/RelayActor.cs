using Akka.Actor;
using Neo.Network.P2P.Payloads;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins
{
    public class RelayActor : UntypedActor
    {
        private readonly NeoSystem neoSystem;
        private readonly FixedDictionary<UInt256, IActorRef> senders;

        public RelayActor(NeoSystem neoSystem, int capacity)
        {
            this.neoSystem = neoSystem;
            senders = new FixedDictionary<UInt256, IActorRef>(capacity);
            Context.System.EventStream.Subscribe(Self, typeof(RelayResult));
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case IInventory inventory:
                    {
                        UInt256 hash = inventory.Hash;
                        senders.Add(hash, Sender);
                        neoSystem.Blockchain.Tell(inventory);
                        break;
                    }
                case RelayResult reason:
                    {
                        UInt256 hash = reason.Inventory.Hash;
                        if (senders.Remove(hash, out var entry))
                        {
                            entry.Tell(reason);
                        }
                        break;
                    }
            }
        }

        public static Props Props(NeoSystem neoSystem, int capacity = 1000)
        {
            return Akka.Actor.Props.Create(() => new RelayActor(neoSystem, capacity));
        }
    }
}
