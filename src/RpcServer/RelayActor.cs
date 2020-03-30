using Akka.Actor;
using Neo.Network.P2P.Payloads;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins
{
    public class RelayActor : UntypedActor
    {
        private readonly IActorRef blockchain;
        private IInventory inventory;
        private IActorRef sender;

        public RelayActor(IActorRef blockchain)
        {
            this.blockchain = blockchain;
            Context.System.EventStream.Subscribe(Self, typeof(RelayResult));
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case IInventory inventory:
                    this.inventory = inventory;
                    this.sender = Sender;
                    blockchain.Tell(inventory);
                    break;
                case RelayResult reason:
                    if (reason.Inventory.Hash.Equals(inventory.Hash))
                        sender.Tell(reason);
                    break;
            }
        }

        public static Props Props(IActorRef blockchain)
        {
            return Akka.Actor.Props.Create(() => new RelayActor(blockchain));
        }
    }
}
