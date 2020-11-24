using Akka.Actor;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;

namespace Neo.Plugins.MPTService
{
    public partial class MPTPlugin : Plugin, IP2PPlugin
    {
        private readonly HashSetCache<UInt256> knownHashes = new HashSetCache<UInt256>(50000);
        public bool OnP2PMessage(Message message)
        {
            if (message.Command != MessageCommand.StateRoot)
                return true;
            StateRoot root = (StateRoot)message.Payload;
            if (root.Witness is null) return false;
            if (!knownHashes.Add(root.Hash)) return false;
            Store?.Tell(root);
            return false;
        }
        public bool OnConsensusMessage(ConsensusPayload payload) => true;
    }
}
