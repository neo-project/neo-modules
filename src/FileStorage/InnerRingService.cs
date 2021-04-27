using Akka.Actor;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.FileStorage.InnerRing;
using Neo.VM;
using System;
using System.Collections.Generic;
using static Neo.FileStorage.InnerRing.InnerRingService;
using FSInnerRingService = Neo.FileStorage.InnerRing.InnerRingService;

namespace Neo.FileStorage
{
    public sealed class InnerRingService : IDisposable
    {
        private readonly IActorRef innering;

        public InnerRingService(NeoSystem main, NeoSystem side)
        {
            innering = main.ActorSystem.ActorOf(FSInnerRingService.Props(side));//TODO: mount to side chain?
            innering.Tell(new Start() { });
        }

        public void OnMainPersisted(Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            foreach (var appExec in applicationExecutedList)
            {
                Transaction tx = appExec.Transaction;
                VMState state = appExec.VMState;
                if (tx is null || state != VMState.HALT) continue;
                var notifys = appExec.Notifications;
                if (notifys is null) continue;
                foreach (var notify in notifys)
                {
                    var contract = notify.ScriptHash;
                    if (contract != Settings.Default.FsContractHash) continue;
                    innering.Tell(new InnerRingSender.MainContractEvent() { notify = notify });
                }
            }
        }

        public void OnSidePersisted(Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            foreach (var appExec in applicationExecutedList)
            {
                Transaction tx = appExec.Transaction;
                VMState state = appExec.VMState;
                if (tx is null || state != VMState.HALT) continue;
                var notifys = appExec.Notifications;
                if (notifys is null) continue;
                foreach (var notify in notifys)
                {
                    var contract = notify.ScriptHash;
                    if (!Settings.Default.Contracts.Contains(contract)) continue;
                    innering.Tell(new MorphContractEvent() { notify = notify });
                }
            }
        }

        public void Dispose()
        {
            innering.Tell(new Stop() { });
        }
    }
}
