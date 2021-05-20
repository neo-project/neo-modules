using System;
using Akka.Actor;
using Cron.Interface;

namespace Cron.Plugins.SyncBlocks
{
    public class PrepareBulkImport
    {
        public IActorRef BlockchainActorRef { get; set; }
        
        public Action OnComplete { get; set; }

        public ICronLogger Logger { get; set; }
        
        public PrepareBulkImport(IActorRef blockchainActorRef, Action onComplete, ICronLogger logger)
        {
            BlockchainActorRef = blockchainActorRef;
            OnComplete = onComplete;
            Logger = logger;
        }
    }
}