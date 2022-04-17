using Akka.Actor;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Ledger.Blockchain;

namespace Neo.Consensus
{
    partial class ConsensusService
    {

        private void SendTxListRequest()
        {
            Log($"Sending {nameof(PrepareRequest)}: height={context.Block.Index} view={context.ViewNumber}");
            localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakeTxListRequest() });

            // In private mode?
            if (context.Validators.Length == 1)
                CheckPreparations();

            //if (context.TransactionHashes.Length > 0)
            //{
            //    foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, context.TransactionHashes))
            //        localNode.Tell(Message.Create(MessageCommand.Inv, payload));
            //}
            ChangeTimer(TimeSpan.FromMilliseconds((neoSystem.Settings.MillisecondsPerBlock << (context.ViewNumber + 1)) - (context.ViewNumber == 0 ? neoSystem.Settings.MillisecondsPerBlock : 0)));
        }

        private void SendPrepareRequest(UInt256[] txs)
        {
            Log($"Sending {nameof(PrepareRequest)}: height={context.Block.Index} view={context.ViewNumber}");
            localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakePrepareRequest(txs) });

            if (context.Validators.Length == 1)
                CheckPreparations();

            if (context.TransactionHashes.Length > 0)
            {
                foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, context.TransactionHashes))
                    localNode.Tell(Message.Create(MessageCommand.Inv, payload));
            }
            ChangeTimer(TimeSpan.FromMilliseconds((neoSystem.Settings.MillisecondsPerBlock << (context.ViewNumber + 1)) - (context.ViewNumber == 0 ? neoSystem.Settings.MillisecondsPerBlock : 0)));
        }

        private void RequestRecovery()
        {
            Log($"Sending {nameof(RecoveryRequest)}: height={context.Block.Index} view={context.ViewNumber} nc={context.CountCommitted} nf={context.CountFailed}");
            localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakeRecoveryRequest() });
        }

        private void RequestChangeView(ChangeViewReason reason)
        {
            if (context.WatchOnly) return;
            // Request for next view is always one view more than the current context.ViewNumber
            // Nodes will not contribute for changing to a view higher than (context.ViewNumber+1), unless they are recovered
            // The latter may happen by nodes in higher views with, at least, `M` proofs
            byte expectedView = context.ViewNumber;
            expectedView++;
            ChangeTimer(TimeSpan.FromMilliseconds(neoSystem.Settings.MillisecondsPerBlock << (expectedView + 1)));
            if ((context.CountCommitted + context.CountFailed) > context.F)
            {
                RequestRecovery();
            }
            else
            {
                Log($"Sending {nameof(ChangeView)}: height={context.Block.Index} view={context.ViewNumber} nv={expectedView} nc={context.CountCommitted} nf={context.CountFailed} reason={reason}");
                localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakeChangeView(reason) });
                CheckExpectedView(expectedView);
            }
        }
    }
}
