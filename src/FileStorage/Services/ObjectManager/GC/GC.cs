using Akka.Actor;
using Neo.FileStorage.Core.Object;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Services.ObjectManager.GC
{
    public class GC : UntypedActor
    {
        private readonly LocalObjectRemover remover;

        public GC(LocalObjectRemover remover)
        {
            this.remover = remover;
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case FSAddress[] addresses:
                    DeleteObjects(addresses);
                    break;
            }
        }

        private void DeleteObjects(FSAddress[] addresses)
        {
            foreach (var address in addresses)
                remover.DeleteObjects(address);
        }

        public static Props Props(LocalObjectRemover remover)
        {
            return Akka.Actor.Props.Create(() => new GC(remover));
        }
    }
}
