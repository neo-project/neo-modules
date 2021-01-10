using Akka.Actor;
using Neo.Network.P2P;
using Neo.Plugins;
using Neo.Wallets;

namespace Neo.Consensus
{
    public class DBFTPlugin : Plugin, IConsensusProvider, IP2PPlugin
    {
        private IActorRef consensus;

        public DBFTPlugin()
        {
            AddService(this);
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        void IConsensusProvider.Start(Wallet wallet)
        {
            consensus = System.ActorSystem.ActorOf(ConsensusService.Props(System.LocalNode, System.TaskManager, System.Blockchain, System.LoadStore("ConsensusState"), wallet));
        }

        bool IP2PPlugin.OnP2PMessage(Message message)
        {
            if (message.Command == MessageCommand.Transaction)
                consensus?.Tell(message.Payload);
            return true;
        }
    }
}
