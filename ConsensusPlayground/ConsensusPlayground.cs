using Neo.Network.P2P.Payloads;
using Neo.Consensus;
using Neo.Network.P2P;
using System;


namespace Neo.Plugins
{
    public class ConsensusPlayground : Plugin, IP2PPlugin
    {
        private static Random RandomGnrt = new Random();

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public bool OnP2PMessage(Message message)
        {
            return true;
        }

        public bool OnConsensusMessage(ConsensusPayload payload)
        {
            if (payload.ConsensusMessage.Type == ConsensusMessageType.PrepareRequest)
                return (RandomGnrt.NextDouble() > Settings.Default.ProbRejectPrepRequest);

            if (payload.ConsensusMessage.Type == ConsensusMessageType.PrepareResponse)
                return (RandomGnrt.NextDouble() > Settings.Default.ProbRejectPrepResponse);
            
            if (payload.ConsensusMessage.Type == ConsensusMessageType.Commit)
                return (RandomGnrt.NextDouble() > Settings.Default.ProbRejectCommit);

            if (payload.ConsensusMessage.Type == ConsensusMessageType.ChangeView)
                return (RandomGnrt.NextDouble() > Settings.Default.ProbRejectChangeView);

            if (payload.ConsensusMessage.Type == ConsensusMessageType.RecoveryMessage)
                return (RandomGnrt.NextDouble() > Settings.Default.ProbRejectRecover);

            return true;
        }   
    }
}
