using Akka.Actor;
using Neo.Network.P2P.Payloads;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins
{
    public class RelayActor : UntypedActor
    {
        private readonly NeoSystem neoSystem;
        private readonly Dictionary<UInt256, IActorRef> senders = new Dictionary<UInt256, IActorRef>();
        private readonly int expireMs;

        public RelayActor(NeoSystem neoSystem, int expireMs)
        {
            this.neoSystem = neoSystem;
            this.expireMs = expireMs;
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
                        // the sender will clean the record after expireMs
                        Task.Run(async () =>
                        {
                            await Task.Delay(expireMs);
                            senders.Remove(hash);
                        });
                        break;
                    }
                case RelayResult reason:
                    {
                        if (senders.Remove(reason.Inventory.Hash, out var actor))
                        {
                            actor.Tell(reason);
                        }
                        break;
                    }
            }
        }

        public static Props Props(NeoSystem neoSystem, int expire = 10000)
        {
            return Akka.Actor.Props.Create(() => new RelayActor(neoSystem, expire));
        }
    }
}
