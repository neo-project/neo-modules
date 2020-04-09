using Akka.Actor;
using System.Collections.ObjectModel;

namespace Neo.Plugins
{
    internal class SendersCollection : KeyedCollection<UInt256, ActorItem>
    {
        private readonly int capacity;

        public SendersCollection(int capacity)
        {
            this.capacity = capacity;
        }

        protected override UInt256 GetKeyForItem(ActorItem item)
        {
            return item.Hash;
        }

        protected override void InsertItem(int index, ActorItem newItem)
        {
            base.InsertItem(index, newItem);

            if (Count > capacity)
            {
                RemoveAt(0);
            }
        }
    }

    public class ActorItem
    {
        public UInt256 Hash { get; set; }

        public IActorRef Actor { get; set; }
    }
}
