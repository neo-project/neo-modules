using System;
using Akka.Actor;

namespace Cron.Plugins.SyncBlocks
{
    public class PrepareBulkImport
    {
        public IActorRef BlockchainActorRef { get; set; }
        
        public Action OnComplete { get; set; }

        public PrepareBulkImport(IActorRef blockchainActorRef, Action onComplete)
        {
            BlockchainActorRef = blockchainActorRef;
            OnComplete = onComplete;
        }
    }
}