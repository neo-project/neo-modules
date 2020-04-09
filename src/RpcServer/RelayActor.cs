using Akka.Actor;
using Neo.Network.P2P.Payloads;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins
{
    public class RelayActor : UntypedActor
    {
        private readonly NeoSystem neoSystem;
        private readonly SendersCollection senders;

        public RelayActor(NeoSystem neoSystem, int capacity)
        {
            this.neoSystem = neoSystem;
            senders = new SendersCollection(capacity);
            Context.System.EventStream.Subscribe(Self, typeof(RelayResult));
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case IInventory inventory:
                    {
                        UInt256 hash = inventory.Hash;
                        senders.Add(new ActorItem { Hash = hash, Actor = Sender });
                        neoSystem.Blockchain.Tell(inventory);
                        break;
                    }
                case RelayResult reason:
                    {
                        UInt256 hash = reason.Inventory.Hash;
                        if (senders.TryGetValue(hash, out var entry))
                        {
                            entry.Actor.Tell(reason);
                            senders.Remove(entry.Hash);
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
