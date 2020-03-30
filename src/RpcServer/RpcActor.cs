using Akka.Actor;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins
{
    public class RpcActor : UntypedActor
    {
        private RelayResult result;

        public RpcActor()
        {
            Context.System.EventStream.Subscribe(Self, typeof(RelayResult));
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case RelayResult reason:
                    result = reason;
                    break;
                case 0:
                    Sender.Tell(result);
                    break;
            }
        }

        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new RpcActor());
        }
    }
}
